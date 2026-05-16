using System.Diagnostics;
using System.Net;
using SpotifyAPI.Web;

namespace SpotifyMount.Services;

public sealed class SpotifyService : IDisposable
{
    private readonly Plugin _plugin;
    private SpotifyClient? _client;
    private CancellationTokenSource? _authCts;

    public bool IsConnected => _client != null;
    public string? DisplayName => _plugin.Config.SpotifyDisplayName;
    public string? CurrentTrack { get; private set; }
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
        try
        {
            var clientId = _plugin.Config.SpotifyClientId!;
            var refreshToken = _plugin.Config.SpotifyRefreshToken!;
            var authenticator = new PKCEAuthenticator(clientId, new PKCETokenResponse { RefreshToken = refreshToken });
            var config = SpotifyClientConfig.CreateDefault().WithAuthenticator(authenticator);
            _client = new SpotifyClient(config);
            var profile = await _client.UserProfile.Current();
            _plugin.Config.SpotifyDisplayName = profile.DisplayName;
            _plugin.SaveConfig();
        }
        catch (Exception ex)
        {
            _plugin.Log.Warning(ex, "Failed to restore Spotify session — re-auth required");
            _client = null;
            _plugin.Config.SpotifyRefreshToken = null;
            _plugin.SaveConfig();
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
                new Uri(redirectUri),
                clientId,
                LoginRequest.ResponseType.Code)
            {
                CodeChallengeMethod = "S256",
                CodeChallenge = challenge,
                Scope = [Scopes.UserReadPlaybackState, Scopes.UserModifyPlaybackState, Scopes.UserReadCurrentlyPlaying]
            };

            Process.Start(new ProcessStartInfo(loginRequest.ToUri().ToString()) { UseShellExecute = true });

            var code = await ListenForCallbackAsync(redirectUri, _authCts.Token);
            if (code == null) return;

            var tokenResponse = await new OAuthClient().RequestToken(
                new PKCETokenRequest(clientId, code, new Uri(redirectUri), verifier));

            var authenticator = new PKCEAuthenticator(clientId, tokenResponse);
            var spotifyConfig = SpotifyClientConfig.CreateDefault().WithAuthenticator(authenticator);
            _client = new SpotifyClient(spotifyConfig);

            _plugin.Config.SpotifyClientId = clientId;
            _plugin.Config.SpotifyRefreshToken = tokenResponse.RefreshToken;

            var profile = await _client.UserProfile.Current();
            _plugin.Config.SpotifyDisplayName = profile.DisplayName;
            _plugin.SaveConfig();

            _plugin.Log.Information("Spotify connected as {Name}", profile.DisplayName);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _plugin.Log.Error(ex, "Spotify auth failed");
        }
        finally
        {
            IsAuthenticating = false;
        }
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

            var responseBytes = "<html><body><h2>Connected! You can close this tab.</h2></body></html>"u8.ToArray();
            ctx.Response.ContentType = "text/html";
            ctx.Response.ContentLength64 = responseBytes.Length;
            await ctx.Response.OutputStream.WriteAsync(responseBytes, ct);
            ctx.Response.Close();
            return code;
        }
        catch { return null; }
        finally { listener.Stop(); }
    }

    public void Disconnect()
    {
        _client = null;
        CurrentTrack = null;
        _plugin.Config.SpotifyRefreshToken = null;
        _plugin.Config.SpotifyDisplayName = null;
        _plugin.SaveConfig();
    }

    public async Task ResumeAsync()
    {
        if (_client == null) return;
        try
        {
            await _client.Player.ResumePlayback();
            await RefreshCurrentTrackAsync();
        }
        catch (Exception ex)
        {
            _plugin.Log.Warning(ex, "Failed to resume Spotify playback");
        }
    }

    public async Task PauseAsync()
    {
        if (_client == null) return;
        try
        {
            await _client.Player.PausePlayback();
            CurrentTrack = null;
        }
        catch (Exception ex)
        {
            _plugin.Log.Warning(ex, "Failed to pause Spotify playback");
        }
    }

    public async Task RefreshCurrentTrackAsync()
    {
        if (_client == null) return;
        try
        {
            var playback = await _client.Player.GetCurrentPlayback();
            if (playback?.Item is FullTrack track)
                CurrentTrack = $"{track.Name} — {string.Join(", ", track.Artists.Select(a => a.Name))}";
            else
                CurrentTrack = null;
        }
        catch { CurrentTrack = null; }
    }

    public void Dispose()
    {
        _authCts?.Cancel();
        _authCts?.Dispose();
    }
}
