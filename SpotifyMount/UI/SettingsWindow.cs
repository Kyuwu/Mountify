using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using System.Numerics;

namespace SpotifyMount.UI;

public sealed class SettingsWindow : Window
{
    private readonly Plugin _plugin;
    private string _clientIdInput = string.Empty;

    public const string RedirectUri = "http://127.0.0.1:5004/callback";

    public SettingsWindow(Plugin plugin) : base("SpotifyMount — Settings##settings")
    {
        _plugin = plugin;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(440, 300),
            MaximumSize = new Vector2(640, 560)
        };
    }

    public override void OnOpen()
    {
        _clientIdInput = _plugin.Config.SpotifyClientId ?? string.Empty;
    }

    public override void Draw()
    {
        var config = _plugin.Config;
        var spotify = _plugin.Spotify;

        ImGui.TextUnformatted("Spotify Connection");
        ImGui.Separator();
        ImGui.Spacing();

        if (spotify.IsConnected)
        {
            ImGui.TextColored(new Vector4(0.3f, 0.9f, 0.4f, 1f), $"Connected as: {spotify.DisplayName ?? "Unknown"}");
            ImGui.Spacing();
            if (ImGui.Button("Disconnect"))
                spotify.Disconnect();
        }
        else
        {
            ImGui.TextDisabled("One-time setup — follow these steps:");
            ImGui.Spacing();

            ImGui.Bullet(); ImGui.SameLine();
            ImGui.TextWrapped("Go to developer.spotify.com → Dashboard → Create app");

            ImGui.Bullet(); ImGui.SameLine();
            ImGui.TextWrapped("Fill in any name and description, leave Website empty.");

            ImGui.Bullet(); ImGui.SameLine();
            ImGui.TextWrapped("Click Save to create the app without a redirect URI.");

            ImGui.Bullet(); ImGui.SameLine();
            ImGui.TextWrapped("Open the app, go to Settings, and add this redirect URI:");

            ImGui.Spacing();
            var uri = RedirectUri;
            ImGui.SetNextItemWidth(-1);
            ImGui.InputText("##redirecturi", ref uri, 64, ImGuiInputTextFlags.ReadOnly);
            ImGui.TextDisabled("  Click Add next to it, then Save. (Use 127.0.0.1, not localhost.)");

            ImGui.Spacing();
            ImGui.Bullet(); ImGui.SameLine();
            ImGui.TextWrapped("Back in the app overview, copy the Client ID and paste it below.");

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.TextUnformatted("Client ID:");
            ImGui.SetNextItemWidth(-1);
            ImGui.InputText("##clientid", ref _clientIdInput, 64);

            ImGui.Spacing();

            var canConnect = !string.IsNullOrWhiteSpace(_clientIdInput) && !spotify.IsAuthenticating;
            if (!canConnect) ImGui.BeginDisabled();
            if (ImGui.Button("Connect — opens browser"))
            {
                config.SpotifyClientId = _clientIdInput.Trim();
                _plugin.SaveConfig();
                _ = spotify.StartAuthAsync(_clientIdInput.Trim());
            }
            if (!canConnect) ImGui.EndDisabled();

            if (spotify.IsAuthenticating)
            {
                ImGui.SameLine();
                ImGui.TextDisabled("Waiting for browser…");
            }
        }

        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.TextUnformatted("Behaviour");
        ImGui.Separator();
        ImGui.Spacing();

        var pause = config.PauseOnDismount;
        if (ImGui.Checkbox("Pause Spotify on dismount", ref pause))
        {
            config.PauseOnDismount = pause;
            _plugin.SaveConfig();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (ImGui.Button("Close"))
            IsOpen = false;
    }
}
