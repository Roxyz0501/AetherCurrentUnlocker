using System.Numerics;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;

namespace AetherCurrentUnlocker.Ipc;

internal sealed class VNavmeshIpc
{
    private readonly ICallGateSubscriber<bool> navReady;
    private readonly ICallGateSubscriber<bool> pathRunning;
    private readonly ICallGateSubscriber<bool> navPathfinding;
    private readonly ICallGateSubscriber<Vector3, Vector3, bool, Task<List<Vector3>>> findPath;
    private readonly ICallGateSubscriber<Vector3, bool, float, Vector3?> pointOnFloor;
    private readonly ICallGateSubscriber<List<Vector3>, bool, object> movePath;
    private readonly ICallGateSubscriber<object> stop;

    public VNavmeshIpc(IDalamudPluginInterface plugin)
    {
        navReady = plugin.GetIpcSubscriber<bool>("vnavmesh.Nav.IsReady");
        pathRunning = plugin.GetIpcSubscriber<bool>("vnavmesh.Path.IsRunning");
        navPathfinding = plugin.GetIpcSubscriber<bool>("vnavmesh.Nav.PathfindInProgress");
        findPath = plugin.GetIpcSubscriber<Vector3, Vector3, bool, Task<List<Vector3>>>("vnavmesh.Nav.Pathfind");
        pointOnFloor = plugin.GetIpcSubscriber<Vector3, bool, float, Vector3?>("vnavmesh.Query.Mesh.PointOnFloor");
        movePath = plugin.GetIpcSubscriber<List<Vector3>, bool, object>("vnavmesh.Path.MoveTo");
        stop = plugin.GetIpcSubscriber<object>("vnavmesh.Path.Stop");
    }

    public bool Available => Safe(() => navReady.HasFunction && findPath.HasFunction && movePath.HasAction);
    public bool Ready => Available && Safe(navReady.InvokeFunc);
    public bool Busy => Available && Safe(() =>
        (pathRunning.HasFunction && pathRunning.InvokeFunc()) ||
        (navPathfinding.HasFunction && navPathfinding.InvokeFunc()));

    public async Task<IReadOnlyList<Vector3>?> FindGroundPath(Vector3 from, Vector3 to)
    {
        if (!Ready || !findPath.HasFunction)
            return null;
        try
        {
            // fly=false: 風脈解放前でも利用できる徒歩・地上マウント経路だけを探索する。
            return await findPath.InvokeFunc(from, to, false).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    public bool FollowGroundPath(IReadOnlyList<Vector3> waypoints)
    {
        if (!Ready || waypoints.Count == 0 || !movePath.HasAction)
            return false;
        try
        {
            // fly=falseを経路探索だけでなく追従側にも明示する。
            movePath.InvokeAction(waypoints.ToList(), false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Stop()
    {
        if (!stop.HasAction)
            return;
        try { stop.InvokeAction(); }
        catch { }
    }

    public Vector3? FindPointOnFloor(Vector3 origin, float searchRadius)
    {
        if (!pointOnFloor.HasFunction)
            return null;
        try { return pointOnFloor.InvokeFunc(origin, false, searchRadius); }
        catch { return null; }
    }

    private static bool Safe(Func<bool> call)
    {
        try { return call(); }
        catch { return false; }
    }
}
