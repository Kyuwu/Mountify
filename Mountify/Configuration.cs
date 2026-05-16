using Dalamud.Configuration;

namespace Mountify;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public bool Enabled { get; set; } = true;

    // Playback — On Mount
    public bool ResumeOnMount { get; set; } = true;
    public bool SmartResume { get; set; } = false;
    public bool SkipToNextOnMount { get; set; } = false;
    public bool ShuffleOnMount { get; set; } = false;
    public int ResumeDelaySeconds { get; set; } = 0;
    public string MountPlaylistUri { get; set; } = string.Empty;

    // Advanced
    public int TrackPollIntervalSeconds { get; set; } = 5;

    // Playback — On Dismount
    public bool PauseOnDismount { get; set; } = true;
    public bool DimOnDismount { get; set; } = false;
    public int DimVolume { get; set; } = 30;
    public bool FadeTransitions { get; set; } = true;
    public int FadeDurationMs { get; set; } = 1200;

    // Volume Override
    public bool MountedVolumeOverride { get; set; } = false;
    public int MountedVolume { get; set; } = 100;

    // Game Audio
    public bool MuteBgmOnMount { get; set; } = true;
    public bool MuteAmbientOnMount { get; set; } = false;
    public int GameAudioRestoreDelaySeconds { get; set; } = 2;

    // Notifications
    public bool ShowTrackInChat { get; set; } = false;
    public bool ShowTrackOnDismount { get; set; } = false;
    public bool ShowPausedInChat { get; set; } = false;
    public string ChatPrefix { get; set; } = "[Mountify]";

    // Auto-Disable
    public bool DisableInCombat { get; set; } = false;
    public bool DisableInPvP { get; set; } = false;
    public bool DisableInInstance { get; set; } = false;
    public bool DisableInCutscene { get; set; } = false;
    public bool DisableWhileCrafting { get; set; } = false;

    public string? SpotifyClientId { get; set; }
    public string? SpotifyRefreshToken { get; set; }
    public string? SpotifyDisplayName { get; set; }
}
