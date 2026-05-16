using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using System.Numerics;

namespace Mountify.UI;

public sealed class MainWindow : Window
{
    private readonly Plugin _plugin;
    private string _clientIdInput = string.Empty;
    private int _volumeDisplay = 100;
    private bool _isDraggingVolume;

    public const string RedirectUri = "http://127.0.0.1:5004/callback";

    public MainWindow(Plugin plugin) : base("Mountify##main")
    {
        _plugin = plugin;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(340, 200),
            MaximumSize = new Vector2(620, 1200)
        };
    }

    public override void OnOpen()
    {
        _clientIdInput = _plugin.Config.SpotifyClientId ?? string.Empty;
    }

    public override void PreDraw()
    {
        ImGui.PushStyleColor(ImGuiCol.WindowBg, Theme.WindowBg);
    }

    public override void PostDraw()
    {
        ImGui.PopStyleColor();
    }

    public override void Draw()
    {
        var config = _plugin.Config;
        var spotify = _plugin.Spotify;
        var mounted = _plugin.Condition[ConditionFlag.Mounted];

        // ── Status ────────────────────────────────────────────────────────────
        using (new CardScope("Status", Theme.Green))
        {
            StatusPill.Draw("##mount",
                mounted ? "Mounted" : "Not Mounted",
                Theme.Green,
                active: mounted);

            ImGui.Spacing();

            if (!spotify.IsConnected)
            {
                StatusPill.Draw("##spotify", "Spotify not connected", Theme.Green, active: false);
            }
            else
            {
                if (spotify.CurrentTrack is { } track)
                {
                    var display = track.Length > 38 ? track[..38] + "…" : track;
                    StatusPill.Draw("##spotify", display, Theme.Green,
                        tooltip: track.Length > 38 ? track : null,
                        icon: FontAwesomeIcon.Music,
                        active: spotify.IsPlaying);
                    ImGui.SameLine(0f, 6f);
                    if (ActionButton.Draw("##refresh", FontAwesomeIcon.SyncAlt, string.Empty, Theme.Green))
                        _ = spotify.RefreshCurrentTrackAsync();
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("Force-refresh current track info from Spotify.");
                }
                else
                {
                    StatusPill.Draw("##spotify", "Connected — not playing", Theme.Green, active: false);
                }

                ImGui.Spacing();

                if (ActionButton.Draw("##prev", FontAwesomeIcon.StepBackward, string.Empty, Theme.Green))
                    _ = spotify.SkipPreviousAsync();
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Previous track  (/mf prev)");

                ImGui.SameLine(0f, 4f);
                var playIcon = spotify.IsPlaying ? FontAwesomeIcon.Pause : FontAwesomeIcon.Play;
                if (ActionButton.Draw("##playpause", playIcon, string.Empty, Theme.Green))
                    _ = spotify.TogglePlaybackAsync();
                if (ImGui.IsItemHovered()) ImGui.SetTooltip(spotify.IsPlaying ? "Pause  (/mf pause)" : "Play  (/mf play)");

                ImGui.SameLine(0f, 4f);
                if (ActionButton.Draw("##next", FontAwesomeIcon.StepForward, string.Empty, Theme.Green))
                    _ = spotify.SkipNextAsync();
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Next track  (/mf next)");

                // ── Live volume slider ─────────────────────────────────────────────
                if (spotify.CurrentVolume.HasValue)
                {
                    ImGui.Spacing();

                    // Sync from Spotify only when the slider is idle
                    if (!_isDraggingVolume)
                        _volumeDisplay = spotify.CurrentVolume.Value;

                    PushSliderStyle();
                    ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                    ImGui.SliderInt("##vol", ref _volumeDisplay, 0, 100, "Volume  %d%%");
                    _isDraggingVolume = ImGui.IsItemActive();
                    if (ImGui.IsItemDeactivatedAfterEdit())
                        _ = spotify.SetVolumeAsync(_volumeDisplay);
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Spotify volume — drag to adjust without opening Spotify.");
                    PopSliderStyle();
                }
            }
        }

        // ── Spotify Connection ─────────────────────────────────────────────────
        using (new CardScope("Spotify Connection", Theme.Green))
        {
            if (spotify.IsConnected)
                DrawConnectedState(spotify);
            else
                DrawSetupGuide(config, spotify);
        }

        // ── Playback ──────────────────────────────────────────────────────────
        using (new CardScope("Playback", Theme.Green))
        {
            Toggle("Enabled##enabled", config.Enabled,
                "Master switch. When off, mounting and dismounting have no effect on Spotify or game audio.",
                v => { config.Enabled = v; _plugin.SaveConfig(); });

            ImGui.Spacing();
            SectionLabel("On Mount");
            ImGui.Spacing();

            Toggle("Resume Spotify when mounted##resume", config.ResumeOnMount,
                "Automatically resumes Spotify playback each time you mount.",
                v => { config.ResumeOnMount = v; _plugin.SaveConfig(); });

            ImGui.Spacing();

            Toggle("Smart resume##smart", config.SmartResume,
                "Only resume if Spotify was already playing when you last dismounted.\nPrevents auto-resume if you manually paused mid-ride.",
                v => { config.SmartResume = v; _plugin.SaveConfig(); });

            ImGui.Spacing();

            Toggle("Skip to next track on mount##skip", config.SkipToNextOnMount,
                "Skips to the next track each time you mount, so every ride starts fresh.",
                v => { config.SkipToNextOnMount = v; _plugin.SaveConfig(); });

            ImGui.Spacing();

            Toggle("Enable shuffle on mount##shuffle", config.ShuffleOnMount,
                "Turns Spotify shuffle on each time you mount.",
                v => { config.ShuffleOnMount = v; _plugin.SaveConfig(); });

            ImGui.Spacing();
            var delay = config.ResumeDelaySeconds;
            DrawSlider("##resumedelay", ref delay, 0, 5,
                delay == 0 ? "Resume immediately" : "%d s delay",
                "Delay before Spotify resumes after mounting.\nUseful to hear the mount fanfare before music kicks in.",
                v => { config.ResumeDelaySeconds = v; _plugin.SaveConfig(); });

            ImGui.Spacing();
            ImGui.TextUnformatted("Mount playlist:");
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            var playlistUri = config.MountPlaylistUri;
            if (ImGui.InputText("##mountplaylist", ref playlistUri, 128))
            {
                config.MountPlaylistUri = playlistUri;
                _plugin.SaveConfig();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Optional Spotify URI to switch to when mounting.\nLeave blank to resume whatever was last playing.\nExample: spotify:playlist:37i9dQZF1DXcBWIGoYBM5M");

            ImGui.Spacing();
            SectionLabel("On Dismount");
            ImGui.Spacing();

            Toggle("Dim volume on dismount##dim", config.DimOnDismount,
                "Lowers Spotify's volume when you dismount instead of pausing.\nMusic keeps playing, just quieter.",
                v => { config.DimOnDismount = v; _plugin.SaveConfig(); });

            if (!config.DimOnDismount)
            {
                ImGui.Spacing();
                Toggle("Pause Spotify on dismount##pause", config.PauseOnDismount,
                    "Pauses Spotify when you dismount. Resumes on next mount if 'Resume when mounted' is on.",
                    v => { config.PauseOnDismount = v; _plugin.SaveConfig(); });
            }
        }

        // ── Volume ────────────────────────────────────────────────────────────
        using (new CardScope("Volume", Theme.Green))
        {
            if (config.DimOnDismount)
            {
                // Paired sliders: show mounted and dismounted volumes together so the
                // user understands both ends of the transition.
                ImGui.TextDisabled("Mounted");
                var mountVol = config.MountedVolume;
                DrawSlider("##mountedvol", ref mountVol, 0, 100, "%d%%",
                    "Spotify volume when you are mounted.\nFades back to this level each time you mount.",
                    v => { config.MountedVolume = v; _plugin.SaveConfig(); });

                ImGui.Spacing();

                ImGui.TextDisabled("Dismounted");
                var dimVol = config.DimVolume;
                DrawSlider("##dimvol", ref dimVol, 0, 100, "%d%%",
                    "Spotify volume when you are dismounted. 0 = silence.\nAlso: /mf vol <n>",
                    v => { config.DimVolume = v; _plugin.SaveConfig(); });

                ImGui.Spacing();

                Toggle("Fade transitions##fade", config.FadeTransitions,
                    "Smoothly fades between the two volume levels instead of jumping instantly.",
                    v => { config.FadeTransitions = v; _plugin.SaveConfig(); });

                if (config.FadeTransitions)
                {
                    ImGui.Spacing();
                    var fadeDur = config.FadeDurationMs;
                    DrawSlider("##fadedur", ref fadeDur, 400, 3000, "%d ms",
                        "Duration of the volume fade. Default: 1200 ms.",
                        v => { config.FadeDurationMs = v; _plugin.SaveConfig(); });
                }

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();
            }

            Toggle("Override Spotify volume on mount##voloverride", config.MountedVolumeOverride,
                "Forces Spotify to a specific volume each time you mount (without dim/restore).\nUseful if you always want a consistent starting volume.",
                v => { config.MountedVolumeOverride = v; _plugin.SaveConfig(); });

            if (config.MountedVolumeOverride && !config.DimOnDismount)
            {
                ImGui.Spacing();
                var ovrVol = config.MountedVolume;
                DrawSlider("##ovrvolume", ref ovrVol, 0, 100, "Set to %d%%",
                    "Volume Spotify will jump to each time you mount.",
                    v => { config.MountedVolume = v; _plugin.SaveConfig(); });
            }
        }

        // ── Game Audio ────────────────────────────────────────────────────────
        using (new CardScope("Game Audio", Theme.Green))
        {
            Toggle("Mute BGM while mounted##mutebgm", config.MuteBgmOnMount,
                "Mutes in-game background music while mounted. BGM restores after dismounting.",
                v =>
                {
                    config.MuteBgmOnMount = v;
                    _plugin.SaveConfig();
                    if (v && mounted)
                    {
                        _plugin.Mount.MuteBgm();
                    }
                    else if (!v)
                    {
                        _plugin.Mount.CancelPendingAudioRestore();
                        _plugin.Mount.RestoreBgm();
                    }
                });

            ImGui.Spacing();

            Toggle("Mute ambient sounds while mounted##muteambient", config.MuteAmbientOnMount,
                "Mutes in-game ambient sounds (wind, rain, city noise) while mounted.",
                v =>
                {
                    config.MuteAmbientOnMount = v;
                    _plugin.SaveConfig();
                    if (v && mounted)
                    {
                        _plugin.Mount.MuteAmbient();
                    }
                    else if (!v)
                    {
                        _plugin.Mount.CancelPendingAudioRestore();
                        _plugin.Mount.RestoreAmbient();
                    }
                });

            if (config.MuteBgmOnMount || config.MuteAmbientOnMount)
            {
                ImGui.Spacing();
                var restoreDelay = config.GameAudioRestoreDelaySeconds;
                DrawSlider("##audiorestore", ref restoreDelay, 0, 8,
                    restoreDelay == 0 ? "Restore instantly" : "%d s restore delay",
                    "How long to wait before restoring game audio after dismounting.\nGives the dismount music time to play before BGM returns.",
                    v => { config.GameAudioRestoreDelaySeconds = v; _plugin.SaveConfig(); });
            }
        }

        // ── Notifications ─────────────────────────────────────────────────────
        using (new CardScope("Notifications", Theme.Green))
        {
            Toggle("Show track in /echo on mount##chat", config.ShowTrackInChat,
                "Prints the current track name to your /echo channel each time you mount.",
                v => { config.ShowTrackInChat = v; _plugin.SaveConfig(); });

            ImGui.Spacing();

            Toggle("Show track in /echo on dismount##chatdismount", config.ShowTrackOnDismount,
                "Prints the track name to /echo when you dismount.",
                v => { config.ShowTrackOnDismount = v; _plugin.SaveConfig(); });

            ImGui.Spacing();

            Toggle("Show paused notification in /echo##chatpause", config.ShowPausedInChat,
                "Prints a 'Paused.' message to /echo when Spotify is paused on dismount.",
                v => { config.ShowPausedInChat = v; _plugin.SaveConfig(); });

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.TextUnformatted("Chat prefix:");
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            var prefix = config.ChatPrefix;
            if (ImGui.InputText("##chatprefix", ref prefix, 32))
            {
                config.ChatPrefix = prefix;
                _plugin.SaveConfig();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Prefix shown before all mount/dismount chat messages.\nDefault: [Mountify]");
        }

        // ── Auto-Disable ──────────────────────────────────────────────────────
        using (new CardScope("Auto-Disable", Theme.Green))
        {
            ImGui.TextDisabled("Suspend Mountify automatically while:");
            ImGui.Spacing();

            Toggle("In combat##combat", config.DisableInCombat,
                "Mounting and dismounting have no effect on Spotify while in combat.",
                v => { config.DisableInCombat = v; _plugin.SaveConfig(); });

            ImGui.Spacing();

            Toggle("In a PvP area##pvp", config.DisableInPvP,
                "Suspends all Mountify behaviour in PvP areas.",
                v => { config.DisableInPvP = v; _plugin.SaveConfig(); });

            ImGui.Spacing();

            Toggle("Inside a duty##instance", config.DisableInInstance,
                "Suspends all Mountify behaviour inside instanced duties.",
                v => { config.DisableInInstance = v; _plugin.SaveConfig(); });

            ImGui.Spacing();

            Toggle("During cutscenes##cutscene", config.DisableInCutscene,
                "Suspends Mountify while a cutscene is playing.",
                v => { config.DisableInCutscene = v; _plugin.SaveConfig(); });

            ImGui.Spacing();

            Toggle("While crafting or gathering##crafting", config.DisableWhileCrafting,
                "Suspends Mountify while you are crafting or gathering.",
                v => { config.DisableWhileCrafting = v; _plugin.SaveConfig(); });
        }

        // ── Advanced ──────────────────────────────────────────────────────────
        using (new CardScope("Advanced", Theme.Green))
        {
            ImGui.TextDisabled("Track status poll interval:");
            var pollInterval = config.TrackPollIntervalSeconds;
            DrawSlider("##pollinterval", ref pollInterval, 2, 30, "%d s",
                "How often Mountify asks Spotify for the current track and playback state.\nLower = more responsive UI; higher = fewer API calls.",
                v => { config.TrackPollIntervalSeconds = v; _plugin.SaveConfig(); });
        }

        // ── Footer ────────────────────────────────────────────────────────────
        ImGui.Spacing();
        ImGui.TextDisabled("/mountify (or /mf)  next · prev · toggle · vol <n> · track · help");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void SectionLabel(string text)
    {
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.TextDisabled(text);
    }

    private static void Toggle(string label, bool value, string tooltip, Action<bool> onChanged)
    {
        ImGui.BeginGroup();
        var changed = SegmentedToggle.Draw(label, ref value, Theme.Green);
        ImGui.EndGroup();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(tooltip);
        if (changed)
            onChanged(value);
    }

    private void DrawSlider(string id, ref int value, int min, int max, string format, string tooltip, Action<int> onChanged)
    {
        PushSliderStyle();
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        if (ImGui.SliderInt(id, ref value, min, max, format))
            onChanged(value);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(tooltip);
        PopSliderStyle();
    }

    private static void PushSliderStyle()
    {
        ImGui.PushStyleColor(ImGuiCol.FrameBg,         new Vector4(0.08f, 0.08f, 0.10f, 1f));
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered,  new Vector4(0.10f, 0.10f, 0.13f, 1f));
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive,   new Vector4(0.12f, 0.12f, 0.16f, 1f));
        ImGui.PushStyleColor(ImGuiCol.SliderGrab,       Theme.Green with { W = 0.85f });
        ImGui.PushStyleColor(ImGuiCol.SliderGrabActive, Theme.Green);
    }

    private static void PopSliderStyle() => ImGui.PopStyleColor(5);

    private void DrawConnectedState(Services.SpotifyService spotify)
    {
        ImGui.TextColored(Theme.Green, $"Connected as:  {spotify.DisplayName ?? "Unknown"}");
        ImGui.Spacing();
        ImGui.Spacing();

        var redAccent = new Vector4(0.9f, 0.32f, 0.32f, 1f);
        if (ActionButton.Draw("##disconnect", FontAwesomeIcon.SignOutAlt, "Disconnect", redAccent))
            spotify.Disconnect();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Removes saved credentials. You will need to re-connect and re-enter your Client ID.");
    }

    private void DrawSetupGuide(Configuration config, Services.SpotifyService spotify)
    {
        ImGui.TextDisabled("One-time setup — follow these steps:");
        ImGui.Spacing();

        ImGui.Bullet(); ImGui.SameLine();
        ImGui.TextWrapped("Go to developer.spotify.com → Dashboard → Create app");

        ImGui.Bullet(); ImGui.SameLine();
        ImGui.TextWrapped("Fill in any name and description, then click Save.");

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
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Paste your Spotify app's Client ID here.\nFound in your app's overview on developer.spotify.com.");

        ImGui.Spacing();

        var canConnect = !string.IsNullOrWhiteSpace(_clientIdInput) && !spotify.IsAuthenticating;
        using (ImRaii.Disabled(!canConnect))
        {
            if (ActionButton.Draw("##connect", FontAwesomeIcon.SignInAlt, "Connect — opens browser", Theme.Green,
                    width: ImGui.GetContentRegionAvail().X))
            {
                config.SpotifyClientId = _clientIdInput.Trim();
                _plugin.SaveConfig();
                _ = spotify.StartAuthAsync(_clientIdInput.Trim());
            }
        }

        if (spotify.IsAuthenticating)
        {
            ImGui.Spacing();
            StatusPill.Draw("##authing", "Waiting for browser…", Theme.Green, active: false);
        }
    }
}
