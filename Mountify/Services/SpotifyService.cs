using System.Diagnostics;
using System.Net;
using SpotifyAPI.Web;

namespace Mountify.Services;

public sealed class SpotifyService : IDisposable
{
    private readonly Plugin _plugin;
    private SpotifyClient? _client;
    private CancellationTokenSource? _authCts;
    private CancellationTokenSource? _fadeCts;
    private bool _isDimmed;
    private int? _lastSetVolume;

    public bool IsConnected => _client != null;
    public bool IsPlaying { get; private set; }
    public string? DisplayName => _plugin.Config.SpotifyDisplayName;
    public string? CurrentTrack { get; private set; }
    public int? CurrentVolume { get; private set; }
    public bool IsAuthenticating { get; private set; }

    public SpotifyService(Plugin plugin)
    {
        _plugin = plugin;

        if (!string.IsNullOrEmpty(plugin.Config.SpotifyRefreshToken) &&
            !string.IsNullOrEmpty(plugin.Config.SpotifyClientId))
        {
            _ = TryRestoreSessionAsync();
        }
    }

    private async Task TryRestoreSessionAsync()
    {
        var clientId = _plugin.Config.SpotifyClientId!;
        var refreshToken = _plugin.Config.SpotifyRefreshToken!;
        try
        {
            // Explicitly refresh so we get a valid access token immediately and
            // catch genuinely-invalid tokens before building the client.
            var tokenResponse = await new OAuthClient().RequestToken(
                new PKCETokenRefreshRequest(clientId, refreshToken));

            _client = new SpotifyClient(BuildConfig(clientId, tokenResponse));

            var profile = await _client.UserProfile.Current();
            _plugin.Config.SpotifyDisplayName = profile.DisplayName;
            _plugin.Config.SpotifyRefreshToken = tokenResponse.RefreshToken;
            _plugin.SaveConfig();
        }
        catch (APIException ex) when ((int?)ex.Response?.StatusCode is 400 or 401)
        {
            // Token is genuinely revoked/invalid — clear it so the user re-auths.
            _plugin.Log.Warning(ex, "Spotify refresh token rejected — re-auth required");
            _client = null;
            _plugin.Config.SpotifyRefreshToken = null;
            _plugin.SaveConfig();
        }
        catch (Exception ex)
        {
            // Transient failure (network down at startup, etc.) — keep the token,
            // it will work next time the plugin loads.
            _plugin.Log.Warning(ex, "Could not restore Spotify session (token preserved, will retry on next load)");
            _client = null;
        }
    }

    public async Task StartAuthAsync(string clientId)
    {
        if (IsAuthenticating) return;
        IsAuthenticating = true;
        _authCts = new CancellationTokenSource();

        try
        {
            const string redirectUri = "http://127.0.0.1:5004/callback";
            var (verifier, challenge) = PKCEUtil.GenerateCodes();

            var loginRequest = new LoginRequest(
                new Uri(redirectUri), clientId, LoginRequest.ResponseType.Code)
            {
                CodeChallengeMethod = "S256",
                CodeChallenge = challenge,
                Scope =
                [
                    Scopes.UserReadPlaybackState,
                    Scopes.UserModifyPlaybackState,
                    Scopes.UserReadCurrentlyPlaying
                ]
            };

            try
            {
                Process.Start(new ProcessStartInfo(loginRequest.ToUri().ToString()) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                _plugin.Log.Warning(ex, "Failed to open browser for Spotify auth");
                return;
            }

            var code = await ListenForCallbackAsync(redirectUri, _authCts.Token);
            if (code == null) return;

            var tokenResponse = await new OAuthClient().RequestToken(
                new PKCETokenRequest(clientId, code, new Uri(redirectUri), verifier));

            _client = new SpotifyClient(BuildConfig(clientId, tokenResponse));

            _plugin.Config.SpotifyClientId = clientId;
            _plugin.Config.SpotifyRefreshToken = tokenResponse.RefreshToken;

            var profile = await _client.UserProfile.Current();
            _plugin.Config.SpotifyDisplayName = profile.DisplayName;
            _plugin.SaveConfig();

            _plugin.Log.Information("Spotify connected as {Name}", profile.DisplayName);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _plugin.Log.Error(ex, "Spotify auth failed"); }
        finally { IsAuthenticating = false; }
    }

    private SpotifyClientConfig BuildConfig(string clientId, PKCETokenResponse tokenResponse)
    {
        var authenticator = new PKCEAuthenticator(clientId, tokenResponse);
        authenticator.TokenRefreshed += (_, newToken) =>
        {
            _plugin.Config.SpotifyRefreshToken = newToken.RefreshToken;
            _plugin.SaveConfig();
        };
        return SpotifyClientConfig.CreateDefault().WithAuthenticator(authenticator);
    }

    private static async Task<string?> ListenForCallbackAsync(string redirectUri, CancellationToken ct)
    {
        using var listener = new HttpListener();
        listener.Prefixes.Add(redirectUri.TrimEnd('/') + "/");
        listener.Start();
        try
        {
            using var reg = ct.Register(listener.Stop);
            var ctx = await listener.GetContextAsync();
            var code = ctx.Request.QueryString["code"];

            var bytes = "<html><body><h2>Connected to Mountify! You can close this tab.</h2></body></html>"u8.ToArray();
            ctx.Response.ContentType = "text/html";
            ctx.Response.ContentLength64 = bytes.Length;
            await ctx.Response.OutputStream.WriteAsync(bytes, ct);
            ctx.Response.Close();
            return code;
        }
        catch { return null; }
        finally { listener.Stop(); }
    }

    public void Disconnect()
    {
        _authCts?.Cancel();
        _authCts?.Dispose();
        _authCts = null;
        _client = null;
        CurrentTrack = null;
        CurrentVolume = null;
        IsPlaying = false;
        _isDimmed = false;
        _lastSetVolume = null;
        _plugin.Config.SpotifyRefreshToken = null;
        _plugin.Config.SpotifyDisplayName = null;
        _plugin.SaveConfig();
    }

    // ── Playback ──────────────────────────────────────────────────────────────

    public async Task ResumeAsync()
    {
        if (_client == null) return;
        try
        {
            await _client.Player.ResumePlayback();
            await RefreshCurrentTrackAsync();
        }
        catch (Exception ex) { _plugin.Log.Warning(ex, "Failed to resume playback"); }
    }

    public async Task ResumeWithPlaylistAsync(string contextUri)
    {
        if (_client == null) return;
        try
        {
            await _client.Player.ResumePlayback(new PlayerResumePlaybackRequest { ContextUri = contextUri });
            await RefreshCurrentTrackAsync();
        }
        catch (Exception ex) { _plugin.Log.Warning(ex, "Failed to resume playlist {Uri}", contextUri); }
    }

    public async Task PauseAsync()
    {
        if (_client == null) return;
        try
        {
            await _client.Player.PausePlayback();
            IsPlaying = false;
        }
        catch (Exception ex) { _plugin.Log.Warning(ex, "Failed to pause playback"); }
    }

    public async Task SetShuffleAsync(bool state)
    {
        if (_client == null) return;
        try { await _client.Player.SetShuffle(new PlayerShuffleRequest(state)); }
        catch (Exception ex) { _plugin.Log.Warning(ex, "Failed to set shuffle"); }
    }

    public async Task TogglePlaybackAsync()
    {
        if (_client == null) return;
        if (IsPlaying) await PauseAsync();
        else await ResumeAsync();
    }

    public async Task SkipNextAsync()
    {
        if (_client == null) return;
        try
        {
            await _client.Player.SkipNext(new PlayerSkipNextRequest());
            await Task.Delay(400);
            await RefreshCurrentTrackAsync();
        }
        catch (Exception ex) { _plugin.Log.Warning(ex, "Failed to skip next"); }
    }

    public async Task SkipPreviousAsync()
    {
        if (_client == null) return;
        try
        {
            await _client.Player.SkipPrevious(new PlayerSkipPreviousRequest());
            await Task.Delay(400);
            await RefreshCurrentTrackAsync();
        }
        catch (Exception ex) { _plugin.Log.Warning(ex, "Failed to skip previous"); }
    }

    public async Task RefreshCurrentTrackAsync()
    {
        if (_client == null) return;
        try
        {
            var playback = await _client.Player.GetCurrentPlayback();
            IsPlaying = playback?.IsPlaying ?? false;
            CurrentVolume = playback?.Device?.VolumePercent;

            if (playback?.Item is FullTrack track)
                CurrentTrack = $"{track.Name} — {string.Join(", ", track.Artists.Select(a => a.Name))}";
            else
                CurrentTrack = null;
        }
        catch (Exception ex) { _plugin.Log.Warning(ex, "Failed to refresh current track"); CurrentTrack = null; IsPlaying = false; }
    }

    // ── Volume ────────────────────────────────────────────────────────────────

    public async Task SetVolumeAsync(int percent)
    {
        if (_client == null) return;
        var clamped = Math.Clamp(percent, 0, 100);
        CurrentVolume = clamped; // optimistic update so the UI reflects the change immediately
        try
        {
            await _client.Player.SetVolume(new PlayerVolumeRequest(clamped));
            _lastSetVolume = clamped;
        }
        catch (Exception ex) { _plugin.Log.Warning(ex, "Failed to set volume to {Vol}%", percent); }
    }

    public async Task DimVolumeAsync(int targetPercent, bool fade)
    {
        _isDimmed = true;
        CancelFade();
        _fadeCts = new CancellationTokenSource();
        var ct = _fadeCts.Token;
        // start from the most accurate known volume: what we last set, or the last polled value
        var from = _lastSetVolume ?? CurrentVolume ?? 100;
        try
        {
            if (fade) await FadeToAsync(from, targetPercent, ct);
            else await SetVolumeAsync(targetPercent);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _plugin.Log.Warning(ex, "Dim volume failed"); }
    }

    public async Task RestoreVolumeAsync(bool fade, int restoreTo)
    {
        if (!_isDimmed) return;
        _isDimmed = false;
        CancelFade();
        _fadeCts = new CancellationTokenSource();
        var ct = _fadeCts.Token;
        // start fade from wherever volume actually is now (accounts for mid-fade cancels)
        var from = _lastSetVolume ?? CurrentVolume ?? _plugin.Config.DimVolume;
        try
        {
            if (fade) await FadeToAsync(from, restoreTo, ct);
            else await SetVolumeAsync(restoreTo);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _plugin.Log.Warning(ex, "Restore volume failed"); }
    }

    private async Task FadeToAsync(int from, int to, CancellationToken ct)
    {
        const int Steps = 8;
        var stepMs = Math.Max(_plugin.Config.FadeDurationMs / Steps, 50);
        for (var i = 1; i <= Steps; i++)
        {
            ct.ThrowIfCancellationRequested();
            var vol = from + (int)Math.Round((to - from) * (i / (double)Steps));
            await SetVolumeAsync(vol);
            await Task.Delay(stepMs, ct);
        }
    }

    private void CancelFade()
    {
        _fadeCts?.Cancel();
        _fadeCts?.Dispose();
        _fadeCts = null;
    }

    public void Dispose()
    {
        _authCts?.Cancel();
        _authCts?.Dispose();
        CancelFade();
    }
}
