using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;

namespace AetherCurrentUnlocker.Ipc;

internal sealed class QuestionableIpc
{
    private readonly ICallGateSubscriber<bool> isRunning;
    private readonly ICallGateSubscriber<string, bool> startSingleQuest;
    private readonly ICallGateSubscriber<string, bool> stop;
    private readonly ICallGateSubscriber<string, bool> isComplete;
    private readonly ICallGateSubscriber<string, bool> isLocked;
    private readonly ICallGateSubscriber<string, (bool, string)> lockedReason;
    private readonly ICallGateSubscriber<string, bool> isAccepted;
    private readonly ICallGateSubscriber<string, bool> isReadyToAccept;

    public QuestionableIpc(IDalamudPluginInterface plugin)
    {
        isRunning = plugin.GetIpcSubscriber<bool>("Questionable.IsRunning");
        startSingleQuest = plugin.GetIpcSubscriber<string, bool>("Questionable.StartSingleQuest");
        stop = plugin.GetIpcSubscriber<string, bool>("Questionable.Stop");
        isComplete = plugin.GetIpcSubscriber<string, bool>("Questionable.IsQuestComplete");
        isLocked = plugin.GetIpcSubscriber<string, bool>("Questionable.IsQuestLocked");
        lockedReason = plugin.GetIpcSubscriber<string, (bool, string)>("Questionable.IsQuestLockedReason");
        isAccepted = plugin.GetIpcSubscriber<string, bool>("Questionable.IsQuestAccepted");
        isReadyToAccept = plugin.GetIpcSubscriber<string, bool>("Questionable.IsReadyToAcceptQuest");
    }

    public bool Available => Safe(() => isRunning.HasFunction && startSingleQuest.HasFunction);
    public bool IsRunning => Available && Safe(isRunning.InvokeFunc);
    public bool IsComplete(ushort questId) => Safe(() => isComplete.InvokeFunc(questId.ToString()));
    public bool IsLocked(ushort questId) => Safe(() => isLocked.InvokeFunc(questId.ToString()), true);
    public bool IsAccepted(ushort questId) => Safe(() => isAccepted.InvokeFunc(questId.ToString()));
    public bool IsReadyToAccept(ushort questId) => Safe(() => isReadyToAccept.InvokeFunc(questId.ToString()));

    public string GetLockedReason(ushort questId)
    {
        try
        {
            if (!lockedReason.HasFunction)
                return L.T("The requirements have not been met.", "開始条件を満たしていません");
            (bool locked, string reason) = lockedReason.InvokeFunc(questId.ToString());
            return locked
                ? (string.IsNullOrWhiteSpace(reason)
                    ? L.T("The requirements have not been met.", "開始条件を満たしていません")
                    : reason)
                : string.Empty;
        }
        catch { return L.T("Could not retrieve requirements from Questionable.", "Questionableから条件を取得できません"); }
    }

    public bool StartSingle(ushort questId) => Safe(() => startSingleQuest.InvokeFunc(questId.ToString()));

    public void StopOwnedRun()
    {
        if (!stop.HasFunction)
            return;
        try { stop.InvokeFunc("Stopped by Aether Current Navigator"); }
        catch { }
    }

    private static bool Safe(Func<bool> call, bool fallback = false)
    {
        try { return call(); }
        catch { return fallback; }
    }
}
