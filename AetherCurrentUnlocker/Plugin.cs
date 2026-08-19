using AetherCurrentUnlocker.Automation;
using AetherCurrentUnlocker.Data;
using AetherCurrentUnlocker.Ipc;
using AetherCurrentUnlocker.Windows;
using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace AetherCurrentUnlocker;

public sealed class Plugin : IDalamudPlugin
{
    private const string Command = "/acnav";
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly ICommandManager commands;
    private readonly IFramework framework;
    private readonly Configuration config;
    private readonly AutomationController automation;
    private readonly MainWindow window;

    public Plugin(
        IDalamudPluginInterface pluginInterface,
        ICommandManager commands,
        IFramework framework,
        IDataManager dataManager,
        IAetheryteList aetheryteList,
        IClientState clientState,
        IPlayerState playerState,
        IObjectTable objects,
        ITargetManager targets,
        ICondition conditions,
        ITextureProvider textureProvider,
        IPluginLog log)
    {
        this.pluginInterface = pluginInterface;
        this.commands = commands;
        this.framework = framework;
        config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Dalamud.Game.ClientLanguage? detectedLanguage;
        try { detectedLanguage = clientState.ClientLanguage; }
        catch { detectedLanguage = null; }
        L.VerifyResolution();
        if (config.Migrate(detectedLanguage))
            pluginInterface.SavePluginConfig(config);
        L.Configure(() => config.DisplayLanguage);

        VNavmeshIpc vnav = new(pluginInterface);
        QuestionableIpc questionable = new(pluginInterface);
        AetherCurrentDataService data = new(dataManager, aetheryteList);
        automation = new(data, vnav, questionable, clientState, objects, targets, conditions,
            () => config.ForCharacter(playerState.ContentId).MountId, log);
        window = new(config, Save, data, automation, vnav, questionable, clientState, playerState, textureProvider,
            pluginInterface, RefreshCommand);

        RegisterCommand();
        framework.Update += OnUpdate;
        pluginInterface.UiBuilder.Draw += window.Draw;
        pluginInterface.UiBuilder.OpenMainUi += OpenWindow;
        pluginInterface.UiBuilder.OpenConfigUi += OpenWindow;
    }

    public string Name => "Aether Current Navigator";

    public void Dispose()
    {
        automation.Dispose();
        framework.Update -= OnUpdate;
        pluginInterface.UiBuilder.Draw -= window.Draw;
        pluginInterface.UiBuilder.OpenMainUi -= OpenWindow;
        pluginInterface.UiBuilder.OpenConfigUi -= OpenWindow;
        commands.RemoveHandler(Command);
    }

    private void OnUpdate(IFramework _) => automation.Tick();

    private void OnCommand(string _, string arguments)
    {
        if (arguments.Trim().Equals("stop", StringComparison.OrdinalIgnoreCase))
            automation.Stop(L.T("Stopped by command.", "コマンドで停止しました"));
        else
            window.IsOpen = true;
    }

    private void OpenWindow() => window.IsOpen = true;
    private void Save() => pluginInterface.SavePluginConfig(config);

    private void RefreshCommand()
    {
        commands.RemoveHandler(Command);
        RegisterCommand();
    }

    private void RegisterCommand() => commands.AddHandler(Command, new CommandInfo(OnCommand)
    {
        HelpMessage = L.T(
            "Opens Aether Current Navigator. Use /acnav stop to stop automation.",
            "Aether Current Navigatorを開きます。/acnav stopで自動処理を停止します。")
    });
}
