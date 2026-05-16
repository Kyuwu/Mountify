using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;

namespace SpotifyMount.Services;

public sealed class MountService : IDisposable
{
    private readonly Plugin _plugin;
    private bool _wasMounted;
    private uint _savedBgmVolume;
    private bool _bgmMuted;

    public MountService(Plugin plugin)
    {
        _plugin = plugin;
    }

    public void OnUpdate(IFramework _)
    {
        var mounted = _plugin.Condition[ConditionFlag.Mounted];
        if (mounted == _wasMounted) return;
        _wasMounted = mounted;

        if (mounted) OnMount();
        else OnDismount();
    }

    private void OnMount()
    {
        if (!_plugin.Config.Enabled) return;
        MuteBgm();
        _ = _plugin.Spotify.ResumeAsync();
    }

    private void OnDismount()
    {
        RestoreBgm();
        if (!_plugin.Config.Enabled) return;
        if (_plugin.Config.PauseOnDismount)
            _ = _plugin.Spotify.PauseAsync();
    }

    private void MuteBgm()
    {
        try
        {
            _plugin.GameConfig.System.TryGet("SoundBgm", out _savedBgmVolume);
            _plugin.GameConfig.System.Set("SoundBgm", 0u);
            _bgmMuted = true;
        }
        catch (Exception ex)
        {
            _plugin.Log.Warning(ex, "Failed to mute BGM");
        }
    }

    private void RestoreBgm()
    {
        if (!_bgmMuted) return;
        try
        {
            _plugin.GameConfig.System.Set("SoundBgm", _savedBgmVolume);
            _bgmMuted = false;
        }
        catch (Exception ex)
        {
            _plugin.Log.Warning(ex, "Failed to restore BGM");
        }
    }

    public void Dispose() => RestoreBgm();
}
