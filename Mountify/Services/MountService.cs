using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;

namespace Mountify.Services;

public sealed class MountService : IDisposable
{
    private readonly Plugin _plugin;
    private bool _wasMounted;
    private uint _savedBgmVolume;
    private bool _bgmMuted;
    private uint _savedAmbientVolume;
    private bool _ambientMuted;
    private bool _wasPlayingWhenDismounted;
    private CancellationTokenSource? _mountCts;
    private CancellationTokenSource? _audioRestoreCts;
    private DateTime _lastPoll = DateTime.MinValue;


    public MountService(Plugin plugin)
    {
        _plugin = plugin;
    }

    public void OnUpdate(IFramework fw)
    {
        var mounted = _plugin.Condition[ConditionFlag.Mounted];
        if (mounted != _wasMounted)
        {
            _wasMounted = mounted;
            _lastPoll = DateTime.MinValue;
            if (mounted) OnMount();
            else OnDismount();
        }

        if (_plugin.Spotify.IsConnected)
        {
            var now = DateTime.UtcNow;
            if ((now - _lastPoll).TotalSeconds >= _plugin.Config.TrackPollIntervalSeconds)
            {
                _lastPoll = now;
                _ = _plugin.Spotify.RefreshCurrentTrackAsync();
            }
        }
    }

    private bool IsSuppressed()
    {
        var cfg = _plugin.Config;
        if (cfg.DisableInCombat      && _plugin.Condition[ConditionFlag.InCombat])    return true;
        if (cfg.DisableInPvP         && _plugin.ClientState.IsPvP)                    return true;
        if (cfg.DisableInInstance    && _plugin.Condition[ConditionFlag.BoundByDuty]) return true;
        if (cfg.DisableInCutscene    && (_plugin.Condition[ConditionFlag.WatchingCutscene] ||
                                          _plugin.Condition[ConditionFlag.OccupiedInCutSceneEvent])) return true;
        if (cfg.DisableWhileCrafting && (_plugin.Condition[ConditionFlag.Crafting] ||
                                          _plugin.Condition[ConditionFlag.Gathering]))  return true;
        return false;
    }

    private void OnMount()
    {
        // Cancel any pending audio restore or prior mount task
        _audioRestoreCts?.Cancel();
        _mountCts?.Cancel();
        _mountCts?.Dispose();
        _mountCts = new CancellationTokenSource();

        // Game audio is always applied regardless of enabled/suppressed state
        if (_plugin.Config.MuteBgmOnMount)     MuteBgm();
        if (_plugin.Config.MuteAmbientOnMount) MuteAmbient();

        if (!_plugin.Config.Enabled) return;
        if (IsSuppressed()) return;

        var cfg = _plugin.Config;

        if (cfg.DimOnDismount)
            _ = _plugin.Spotify.RestoreVolumeAsync(cfg.FadeTransitions, cfg.MountedVolume);
        else if (cfg.MountedVolumeOverride)
            _ = _plugin.Spotify.SetVolumeAsync(cfg.MountedVolume);

        if (cfg.ResumeOnMount && (!cfg.SmartResume || _wasPlayingWhenDismounted))
            _ = OnMountAsync(_mountCts.Token);
    }

    private async Task OnMountAsync(CancellationToken ct)
    {
        var cfg = _plugin.Config;

        if (cfg.ResumeDelaySeconds > 0)
        {
            try { await Task.Delay(cfg.ResumeDelaySeconds * 1000, ct); }
            catch (OperationCanceledException) { return; }
        }

        if (ct.IsCancellationRequested) return;

        if (cfg.ShuffleOnMount)
            await _plugin.Spotify.SetShuffleAsync(true);

        if (!string.IsNullOrWhiteSpace(cfg.MountPlaylistUri))
            await _plugin.Spotify.ResumeWithPlaylistAsync(cfg.MountPlaylistUri);
        else if (cfg.SkipToNextOnMount)
        {
            await _plugin.Spotify.SkipNextAsync();
            await _plugin.Spotify.ResumeAsync();
        }
        else
            await _plugin.Spotify.ResumeAsync();

        if (cfg.ShowTrackInChat && _plugin.Spotify.CurrentTrack is { } track)
            _plugin.ChatGui.Print($"{cfg.ChatPrefix} Now playing: {track}");
    }

    private void OnDismount()
    {
        // Cancel any in-progress mount task (e.g. resume delay)
        _mountCts?.Cancel();

        // Start delayed audio restore so dismount music can play first
        if (_plugin.Config.MuteBgmOnMount || _plugin.Config.MuteAmbientOnMount)
        {
            _audioRestoreCts?.Cancel();
            _audioRestoreCts?.Dispose();
            _audioRestoreCts = new CancellationTokenSource();
            _ = DelayedAudioRestoreAsync(_audioRestoreCts.Token);
        }

        if (!_plugin.Config.Enabled) return;
        if (IsSuppressed()) return;

        var cfg = _plugin.Config;
        _wasPlayingWhenDismounted = _plugin.Spotify.IsPlaying;

        if (cfg.ShowTrackOnDismount && _plugin.Spotify.CurrentTrack is { } track)
            _plugin.ChatGui.Print($"{cfg.ChatPrefix} Was playing: {track}");

        if (cfg.DimOnDismount)
            _ = _plugin.Spotify.DimVolumeAsync(cfg.DimVolume, cfg.FadeTransitions);
        else if (cfg.PauseOnDismount)
        {
            _ = _plugin.Spotify.PauseAsync();
            if (cfg.ShowPausedInChat)
                _plugin.ChatGui.Print($"{cfg.ChatPrefix} Paused.");
        }
    }

    private async Task DelayedAudioRestoreAsync(CancellationToken ct)
    {
        var delay = _plugin.Config.GameAudioRestoreDelaySeconds;
        if (delay > 0)
        {
            try { await Task.Delay(delay * 1000, ct); }
            catch (OperationCanceledException) { return; }
        }
        if (ct.IsCancellationRequested) return;
        if (_plugin.Config.MuteBgmOnMount)     RestoreBgm();
        if (_plugin.Config.MuteAmbientOnMount) RestoreAmbient();
    }

    // ── Game Audio ────────────────────────────────────────────────────────────

    public void MuteBgm()
    {
        if (_bgmMuted) return;
        try
        {
            _plugin.GameConfig.System.TryGet("SoundBgm", out _savedBgmVolume);
            _plugin.GameConfig.System.Set("SoundBgm", 0u);
            _bgmMuted = true;
        }
        catch (Exception ex) { _plugin.Log.Warning(ex, "Failed to mute BGM"); }
    }

    public void RestoreBgm()
    {
        if (!_bgmMuted) return;
        try
        {
            _plugin.GameConfig.System.Set("SoundBgm", _savedBgmVolume);
            _bgmMuted = false;
        }
        catch (Exception ex) { _plugin.Log.Warning(ex, "Failed to restore BGM"); }
    }

    public void MuteAmbient()
    {
        if (_ambientMuted) return;
        try
        {
            _plugin.GameConfig.System.TryGet("SoundAmbient", out _savedAmbientVolume);
            _plugin.GameConfig.System.Set("SoundAmbient", 0u);
            _ambientMuted = true;
        }
        catch (Exception ex) { _plugin.Log.Warning(ex, "Failed to mute ambient"); }
    }

    public void RestoreAmbient()
    {
        if (!_ambientMuted) return;
        try
        {
            _plugin.GameConfig.System.Set("SoundAmbient", _savedAmbientVolume);
            _ambientMuted = false;
        }
        catch (Exception ex) { _plugin.Log.Warning(ex, "Failed to restore ambient"); }
    }

    public void CancelPendingAudioRestore()
    {
        _audioRestoreCts?.Cancel();
    }

    public void Dispose()
    {
        _mountCts?.Cancel();
        _mountCts?.Dispose();
        _audioRestoreCts?.Cancel();
        _audioRestoreCts?.Dispose();
        RestoreBgm();
        RestoreAmbient();
    }
}
