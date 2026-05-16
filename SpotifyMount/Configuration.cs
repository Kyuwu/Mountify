using Dalamud.Configuration;

namespace SpotifyMount;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public bool Enabled { get; set; } = true;
    public bool PauseOnDismount { get; set; } = true;

    public string? SpotifyClientId { get; set; }
    public string? SpotifyRefreshToken { get; set; }
    public string? SpotifyDisplayName { get; set; }
}
