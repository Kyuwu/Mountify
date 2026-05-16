using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Mountify.Services;
using Mountify.UI;

namespace Mountify;

public sealed class Plugin : IAsyncDalamudPlugin
{
    internal readonly IDalamudPluginInterface PluginInterface;
    internal readonly ICommandManager CommandManager;
    internal readonly IFramework Framework;
    internal readonly ICondition Condition;
    internal readonly IClientState ClientState;
    internal readonly IGameConfig GameConfig;
    internal readonly IChatGui ChatGui;
    internal readonly IPluginLog Log;

    private const string CommandName = "/mountify";
    private const string CommandAlias = "/mf";

    internal Configuration Config = null!;
    internal SpotifyService Spotify = null!;
    internal MountService Mount = null!;
    internal MainWindow MainWindow = null!;

    private readonly WindowSystem _windowSystem = new("Mountify");

    public Plugin(
        IDalamudPluginInterface pluginInterface,
        ICommandManager commandManager,
        IFramework framework,
        ICondition condition,
        IClientState clientState,
        IGameConfig gameConfig,
        IChatGui chatGui,
        IPluginLog log)
    {
        PluginInterface = pluginInterface;
        CommandManager = commandManager;
        Framework = framework;
        Condition = condition;
        ClientState = clientState;
        GameConfig = gameConfig;
        ChatGui = chatGui;
        Log = log;
    }

    public Task LoadAsync(CancellationToken cancellationToken)
    {
        Config = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        Spotify = new SpotifyService(this);
        Mount = new MountService(this);
        MainWindow = new MainWindow(this);
        _windowSystem.AddWindow(MainWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Toggle the Mountify window. Subcommands: next, prev, pause, play, toggle, vol <0-100>, track, enable, disable, help."
        });
        CommandManager.AddHandler(CommandAlias, new CommandInfo(OnCommand)
        {
            HelpMessage = "Alias for /mountify."
        });

        Framework.Update += Mount.OnUpdate;
        PluginInterface.UiBuilder.Draw += _windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi += MainWindow.Toggle;
        PluginInterface.UiBuilder.OpenConfigUi += MainWindow.Toggle;

        return Task.CompletedTask;
    }

    private void OnCommand(string command, string args)
    {
        var arg = args.Trim();
        var lower = arg.ToLowerInvariant();

        switch (lower)
        {
            case "":
                MainWindow.Toggle();
                return;

            case "next":
                RequireSpotify(() =>
                {
                    _ = Spotify.SkipNextAsync();
                    ChatGui.Print("[Mountify] Skipping to next track…");
                });
                return;

            case "prev":
            case "previous":
                RequireSpotify(() =>
                {
                    _ = Spotify.SkipPreviousAsync();
                    ChatGui.Print("[Mountify] Skipping to previous track…");
                });
                return;

            case "pause":
                RequireSpotify(() =>
                {
                    _ = Spotify.PauseAsync();
                    ChatGui.Print("[Mountify] Paused.");
                });
                return;

            case "play":
                RequireSpotify(() =>
                {
                    _ = Spotify.ResumeAsync();
                    ChatGui.Print("[Mountify] Resumed.");
                });
                return;

            case "toggle":
                RequireSpotify(() =>
                {
                    _ = Spotify.TogglePlaybackAsync();
                    ChatGui.Print(Spotify.IsPlaying ? "[Mountify] Paused." : "[Mountify] Resumed.");
                });
                return;

            case "track":
                if (Spotify.CurrentTrack is { } t)
                    ChatGui.Print($"[Mountify] Now playing: {t}");
                else
                    ChatGui.Print("[Mountify] Nothing is playing.");
                return;

            case "enable":
                Config.Enabled = true;
                SaveConfig();
                ChatGui.Print("[Mountify] Enabled.");
                return;

            case "disable":
                Config.Enabled = false;
                SaveConfig();
                ChatGui.Print("[Mountify] Disabled.");
                return;

            case "help":
                PrintHelp();
                return;
        }

        if (lower.StartsWith("vol "))
        {
            var volStr = arg[4..].Trim();
            if (int.TryParse(volStr, out var vol) && vol is >= 0 and <= 100)
            {
                RequireSpotify(() =>
                {
                    _ = Spotify.SetVolumeAsync(vol);
                    ChatGui.Print($"[Mountify] Volume set to {vol}%.");
                });
            }
            else
            {
                ChatGui.Print("[Mountify] Usage: /mountify vol <0-100>");
            }
            return;
        }

        ChatGui.Print($"[Mountify] Unknown subcommand '{arg}'. Type /mountify help for a list.");
    }

    private void RequireSpotify(Action action)
    {
        if (!Spotify.IsConnected)
            ChatGui.Print("[Mountify] Not connected to Spotify.");
        else
            action();
    }

    private void PrintHelp()
    {
        ChatGui.Print("[Mountify] Available subcommands:");
        ChatGui.Print("  (no args)         — open/close the window");
        ChatGui.Print("  next / prev       — skip tracks");
        ChatGui.Print("  pause / play      — pause or resume");
        ChatGui.Print("  toggle            — toggle play/pause");
        ChatGui.Print("  vol <0-100>       — set Spotify volume");
        ChatGui.Print("  track             — print current track to chat");
        ChatGui.Print("  enable / disable  — turn Mountify on or off");
    }

    public void SaveConfig() => PluginInterface.SavePluginConfig(Config);

    public ValueTask DisposeAsync()
    {
        Framework.Update -= Mount.OnUpdate;
        PluginInterface.UiBuilder.Draw -= _windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= MainWindow.Toggle;
        PluginInterface.UiBuilder.OpenConfigUi -= MainWindow.Toggle;

        CommandManager.RemoveHandler(CommandName);
        CommandManager.RemoveHandler(CommandAlias);
        _windowSystem.RemoveAllWindows();
        Mount.Dispose();
        Spotify.Dispose();
        return ValueTask.CompletedTask;
    }
}
