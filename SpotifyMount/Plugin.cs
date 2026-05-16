using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using SpotifyMount.Services;
using SpotifyMount.UI;

namespace SpotifyMount;

public sealed class Plugin : IAsyncDalamudPlugin
{
    internal readonly IDalamudPluginInterface PluginInterface;
    internal readonly ICommandManager CommandManager;
    internal readonly IFramework Framework;
    internal readonly ICondition Condition;
    internal readonly IGameConfig GameConfig;
    internal readonly IPluginLog Log;

    private const string CommandName = "/spotifymount";

    internal Configuration Config = null!;
    internal SpotifyService Spotify = null!;
    internal MainWindow MainWindow = null!;
    internal SettingsWindow SettingsWindow = null!;

    private MountService _mount = null!;
    private readonly WindowSystem _windowSystem = new("SpotifyMount");

    public Plugin(
        IDalamudPluginInterface pluginInterface,
        ICommandManager commandManager,
        IFramework framework,
        ICondition condition,
        IGameConfig gameConfig,
        IPluginLog log)
    {
        PluginInterface = pluginInterface;
        CommandManager = commandManager;
        Framework = framework;
        Condition = condition;
        GameConfig = gameConfig;
        Log = log;
    }

    public Task LoadAsync(CancellationToken cancellationToken)
    {
        Config = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        Spotify = new SpotifyService(this);
        _mount = new MountService(this);

        MainWindow = new MainWindow(this);
        SettingsWindow = new SettingsWindow(this);
        _windowSystem.AddWindow(MainWindow);
        _windowSystem.AddWindow(SettingsWindow);

        CommandManager.AddHandler(CommandName, new Dalamud.Game.Command.CommandInfo(OnCommand)
        {
            HelpMessage = "Toggle the SpotifyMount status window."
        });

        Framework.Update += _mount.OnUpdate;
        PluginInterface.UiBuilder.Draw += _windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi += MainWindow.Toggle;
        PluginInterface.UiBuilder.OpenConfigUi += SettingsWindow.Toggle;

        return Task.CompletedTask;
    }

    private void OnCommand(string command, string args) => MainWindow.Toggle();

    public void SaveConfig() => PluginInterface.SavePluginConfig(Config);

    public ValueTask DisposeAsync()
    {
        Framework.Update -= _mount.OnUpdate;
        PluginInterface.UiBuilder.Draw -= _windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= MainWindow.Toggle;
        PluginInterface.UiBuilder.OpenConfigUi -= SettingsWindow.Toggle;

        CommandManager.RemoveHandler(CommandName);
        _windowSystem.RemoveAllWindows();
        _mount.Dispose();
        Spotify.Dispose();
        return ValueTask.CompletedTask;
    }
}
