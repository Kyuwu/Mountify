using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface.Windowing;
using System.Numerics;

namespace SpotifyMount.UI;

public sealed class MainWindow : Window
{
    private readonly Plugin _plugin;

    public MainWindow(Plugin plugin) : base("SpotifyMount##main", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        _plugin = plugin;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(280, 120),
            MaximumSize = new Vector2(480, 300)
        };
    }

    public override void Draw()
    {
        var config = _plugin.Config;

        var enabled = config.Enabled;
        if (ImGui.Checkbox("Enabled", ref enabled))
        {
            config.Enabled = enabled;
            _plugin.SaveConfig();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        var mounted = _plugin.Condition[ConditionFlag.Mounted];
        ImGui.TextUnformatted("Status: ");
        ImGui.SameLine();
        if (mounted)
            ImGui.TextColored(new Vector4(0.4f, 1f, 0.4f, 1f), "Mounted");
        else
            ImGui.TextDisabled("Not mounted");

        ImGui.Spacing();

        var spotify = _plugin.Spotify;
        ImGui.TextUnformatted("Spotify: ");
        ImGui.SameLine();
        if (!spotify.IsConnected)
            ImGui.TextDisabled("Not connected");
        else if (spotify.CurrentTrack != null)
            ImGui.TextColored(new Vector4(0.3f, 0.9f, 0.4f, 1f), spotify.CurrentTrack);
        else
            ImGui.TextDisabled("Connected — not playing");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (ImGui.Button("Settings"))
            _plugin.SettingsWindow.Toggle();

        if (spotify.IsConnected)
        {
            ImGui.SameLine();
            if (ImGui.Button("Refresh track"))
                _ = spotify.RefreshCurrentTrackAsync();
        }
    }
}
