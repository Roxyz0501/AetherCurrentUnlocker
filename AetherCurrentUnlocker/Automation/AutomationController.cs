using System.Numerics;
using AetherCurrentUnlocker.Data;
using AetherCurrentUnlocker.Ipc;
using AetherCurrentUnlocker.Models;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using GameObjectStruct = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace AetherCurrentUnlocker.Automation;

internal sealed class AutomationController : IDisposable
{
    private sealed record GroundRoute(IReadOnlyList<Vector3> Waypoints, float Length);
    private sealed record DeparturePlan(bool UseCurrentPosition, int AetheryteIndex, GroundRoute Route);

    private readonly AetherCurrentDataService data;
    private readonly VNavmeshIpc vnav;
    private readonly QuestionableIpc questionable;
    private readonly IClientState clientState;
    private readonly IObjectTable objects;
    private readonly ITargetManager targets;
    private readonly ICondition conditions;
    private readonly Func<uint> selectedMountProvider;
    private readonly IPluginLog log;

    private readonly HashSet<uint> blockedCurrents = [];
    private readonly HashSet<ushort> blockedQuests = [];
    private readonly HashSet<(uint Id, byte SubIndex)> attemptedAetherytesForTarget = [];
    private readonly Queue<TerritoryProgress> territoryQueue = [];
    private TerritoryProgress? activeTerritory;
    private int totalTerritories;
    private int completedTerritories;
    private FieldCurrent? current;
    private ushort? questId;
    private ushort? questAwaitingStart;
    private DateTime phaseStarted;
    private DateTime nextActionAt;
    private DateTime nextMountAttemptAt;
    private DateTime teleportArrivalDeadline;
    private DateTime teleportMovementReleaseAt;
    private DateTime lastMovementProgressAt;
    private Vector3 lastMovementProgressPosition;
    private Vector3? recoveryWaypoint;
    private Task<IReadOnlyList<Vector3>?>? pendingGroundPath;
    private Task<DeparturePlan?>? departurePlanTask;
    private IReadOnlyList<Vector3>? preparedGroundPath;
    private Vector3 pendingGroundDestination;
    private IReadOnlyList<TeleportDestination> aetheryteCandidates = [];
    private TeleportDestination? forcedTeleportDestination;
    private TeleportDestination? pendingTeleportArrival;
    private int aetheryteCandidateIndex;
    private int pathRecalculationCount;
    private int mountAttemptCount;
    private bool ownsMovement;
    private bool ownsQuestionable;
    private bool targetTeleportHandled;
    private bool departureSelectionComplete;
    private bool wasMounted;
    private bool mountWasTemporarilyBlocked;
    private bool includeFields;
    private bool includeQuests;
    private bool paused;
    private uint? singleFieldId;
    private ushort? singleQuestId;
    private (uint Id, byte SubIndex)? lastSuccessfulTeleport;

    public AutomationController(
        AetherCurrentDataService data,
        VNavmeshIpc vnav,
        QuestionableIpc questionable,
        IClientState clientState,
        IObjectTable objects,
        ITargetManager targets,
        ICondition conditions,
        Func<uint> selectedMountProvider,
        IPluginLog log)
    {
        this.data = data;
        this.vnav = vnav;
        this.questionable = questionable;
        this.clientState = clientState;
        this.objects = objects;
        this.targets = targets;
        this.conditions = conditions;
        this.selectedMountProvider = selectedMountProvider;
        this.log = log;
    }

    public AutomationMode Mode { get; private set; }
    public AutomationPhase Phase { get; private set; } = AutomationPhase.Idle;
    public string Status { get; private set; } = L.T("Idle", "停止中");
    public string LastIssue { get; private set; } = string.Empty;
    public bool IsRunning => Mode != AutomationMode.None;
    public bool IsPaused => paused;
    public bool CanPause => IsRunning && !paused && questId == null && pendingTeleportArrival == null;
    public FieldCurrent? CurrentTarget => current;
    public ushort? CurrentQuestId => questId;
    public bool CanRecalculatePath => IsRunning && current != null && clientState.TerritoryType == current.TerritoryId;
    public bool IsPossiblyStuck { get; private set; }
    public int PathRecalculationCount => pathRecalculationCount;
    public string? ActiveTerritoryName => activeTerritory?.Name;
    public int CompletedTerritories => completedTerritories;
    public int TotalTerritories => totalTerritories;
    public int CurrentAetheryteCandidate => aetheryteCandidateIndex < 0 ? 0 : aetheryteCandidateIndex + 1;
    public int AetheryteCandidateCount => aetheryteCandidates.Count;
    public string? CurrentAetheryteName => aetheryteCandidateIndex < 0
        ? L.T("Current location", "現在地点")
        : aetheryteCandidates.ElementAtOrDefault(aetheryteCandidateIndex)?.Name;
    public uint? CurrentAetheryteId => aetheryteCandidateIndex < 0
        ? null
        : aetheryteCandidates.ElementAtOrDefault(aetheryteCandidateIndex)?.AetheryteId;
    public float? CurrentAetheryteHorizontalDistance
    {
        get
        {
            if (aetheryteCandidateIndex < 0)
                return null;
            TeleportDestination? destination = aetheryteCandidates.ElementAtOrDefault(aetheryteCandidateIndex);
            if (destination == null || current == null)
                return null;
            float dx = destination.Position.X - current.Position.X;
            float dz = destination.Position.Z - current.Position.Z;
            return MathF.Sqrt(dx * dx + dz * dz);
        }
    }
    public bool HasNextAetheryteCandidate => aetheryteCandidates.Any(x =>
    {
        var key = (x.AetheryteId, x.SubIndex);
        return !attemptedAetherytesForTarget.Contains(key) && lastSuccessfulTeleport != key;
    });

    public bool StartCurrentTerritory(bool fields, bool quests)
    {
        TerritoryProgress? territory = data.GetTerritory(clientState.TerritoryType);
        if (territory == null)
            return Reject(L.T("The current area has no Aether Current data.", "現在のエリアには風脈がありません。"));
        return Start(AutomationMode.CurrentTerritory, [territory], fields, quests);
    }

    public bool StartExpansion(uint expansionId, bool fields, bool quests)
    {
        IReadOnlyList<TerritoryProgress> territories = data.GetExpansion(expansionId);
        if (territories.Count == 0)
            return Reject(L.T("No Aether Current data is available for the selected expansion.", "指定した拡張の風脈データがありません。"));
        return Start(AutomationMode.Expansion, territories, fields, quests);
    }

    public bool StartSingleField(FieldCurrent field)
    {
        TerritoryProgress? territory = data.GetTerritory(field.TerritoryId);
        if (territory == null)
            return Reject(L.T("The selected field Aether Current has no data.", "対象のフィールド風脈データがありません。"));
        if (!Start(AutomationMode.SingleTarget, [territory], fields: true, quests: false))
            return false;
        singleFieldId = field.CurrentId;
        return true;
    }

    public bool StartSingleQuest(ushort id, uint territoryId)
    {
        TerritoryProgress? territory = data.GetTerritory(territoryId);
        if (territory == null || !territory.QuestIds.Contains(id))
            return Reject(L.T("The selected Aether Current quest has no data.", "対象の風脈クエストデータがありません。"));
        if (!vnav.Available)
            return Reject(L.T("vnavmesh is required to select a ground route to the quest issuer.", "クエスト受注地点への地上経路選択には vnavmesh が必要です。"));
        if (!Start(AutomationMode.SingleTarget, [territory], fields: false, quests: true))
            return false;
        singleQuestId = id;
        return true;
    }

    private bool Start(AutomationMode mode, IReadOnlyList<TerritoryProgress> territories, bool fields, bool quests)
    {
        if (IsRunning)
            return Reject(L.T("Automation is already running.", "すでに実行中です。"));
        if (!fields && !quests)
            return Reject(L.T("Select field Aether Currents, Aether Current quests, or both.", "フィールド風脈または風脈クエストを選択してください。"));
        if (fields && !vnav.Available)
            return Reject(L.T("vnavmesh is required to travel to field Aether Currents.", "フィールド風脈の移動には vnavmesh が必要です。"));
        if (quests && !questionable.Available)
            return Reject(L.T("Questionable is required to run Aether Current quests.", "風脈クエストの進行には Questionable が必要です。"));
        if (questionable.IsRunning)
            return Reject(L.T("Questionable is already running. Stop it first.", "Questionable がすでに実行中です。先に停止してください。"));

        IEnumerable<TerritoryProgress> orderedTerritories = mode == AutomationMode.Expansion
            ? territories.OrderByDescending(x => x.TerritoryId == clientState.TerritoryType).ThenBy(x => x.TerritoryId)
            : territories;
        territoryQueue.Clear();
        foreach (TerritoryProgress territory in orderedTerritories)
            territoryQueue.Enqueue(territory);
        activeTerritory = null;
        totalTerritories = territoryQueue.Count;
        completedTerritories = 0;
        includeFields = fields;
        includeQuests = quests;
        singleFieldId = null;
        singleQuestId = null;
        blockedCurrents.Clear();
        blockedQuests.Clear();
        current = null;
        questId = null;
        LastIssue = string.Empty;
        paused = false;
        Mode = mode;
        SetPhase(AutomationPhase.Selecting, L.T("Selecting a target", "対象を選択中"));
        return true;
    }

    public void Tick()
    {
        if (!IsRunning)
            return;
        if (paused)
            return;

        if (objects.LocalPlayer == null)
        {
            PauseMovement(L.T("Waiting for player information", "プレイヤー情報を待っています"));
            return;
        }

        if (conditions[ConditionFlag.BetweenAreas] || conditions[ConditionFlag.BetweenAreas51])
        {
            PauseMovement(L.T("Waiting for area transition", "エリア移動を待っています"));
            return;
        }

        bool mountingWithoutStopping = mountAttemptCount > 0 && !conditions[ConditionFlag.Mounted];
        bool fieldMovementBlocked = conditions[ConditionFlag.Unconscious] ||
                                    conditions[ConditionFlag.Occupied] ||
                                    conditions[ConditionFlag.OccupiedInQuestEvent] ||
                                    conditions[ConditionFlag.OccupiedInCutSceneEvent] ||
                                    conditions[ConditionFlag.WatchingCutscene] ||
                                    conditions[ConditionFlag.WatchingCutscene78] ||
                                    conditions[ConditionFlag.Jumping] || conditions[ConditionFlag.Jumping61] ||
                                    (!mountingWithoutStopping &&
                                     (conditions[ConditionFlag.Casting] || conditions[ConditionFlag.Casting87] ||
                                      conditions[ConditionFlag.MountOrOrnamentTransition]));
        if (current != null && fieldMovementBlocked)
        {
            PauseMovement(L.T("Waiting until the character can be controlled", "キャラクターが操作可能になるまで待っています"));
            return;
        }

        if (questId.HasValue)
            TickQuest();
        else if (current != null)
            TickFieldCurrent();
        else
            SelectNext();
    }

    public void Stop(string? reason = null)
    {
        StopOwnedMovement();
        if (ownsQuestionable)
            questionable.StopOwnedRun();
        ownsQuestionable = false;
        paused = false;
        pendingTeleportArrival = null;
        teleportArrivalDeadline = default;
        teleportMovementReleaseAt = default;
        current = null;
        questId = null;
        questAwaitingStart = null;
        territoryQueue.Clear();
        activeTerritory = null;
        Mode = AutomationMode.None;
        SetPhase(AutomationPhase.Stopped, reason ?? L.T("Stopped", "停止しました"));
    }

    public void Dispose() => Stop(L.T("Plugin unloaded", "プラグインを終了しました"));

    public bool Pause()
    {
        if (!CanPause)
            return false;
        StopOwnedMovement();
        paused = true;
        SetPhase(AutomationPhase.Waiting, L.T("Paused", "一時停止中"));
        return true;
    }

    public bool Resume()
    {
        if (!IsRunning || !paused)
            return false;
        paused = false;
        DateTime now = DateTime.UtcNow;
        nextActionAt = now;
        phaseStarted = now;
        lastMovementProgressAt = now;
        lastMovementProgressPosition = objects.LocalPlayer?.Position ?? default;
        IsPossiblyStuck = false;
        SetPhase(AutomationPhase.Waiting, current == null
            ? L.T("Resuming target selection", "対象選択を再開します")
            : L.T("Resuming travel", "移動を再開します"));
        return true;
    }

    private void SelectNext()
    {
        if (activeTerritory == null)
        {
            if (territoryQueue.Count == 0)
            {
                string suffix = blockedCurrents.Count + blockedQuests.Count > 0
                    ? L.T($" ({blockedCurrents.Count + blockedQuests.Count} unavailable; review the details)",
                        $"（実行不能 {blockedCurrents.Count + blockedQuests.Count} 件。詳細を確認してください）")
                    : string.Empty;
                Mode = AutomationMode.None;
                SetPhase(AutomationPhase.Completed, L.T("Finished processing the selected scope", "対象範囲の処理が完了しました") + suffix);
                return;
            }

            activeTerritory = territoryQueue.Dequeue();
            SetPhase(AutomationPhase.Selecting, L.T(
                $"Area {completedTerritories + 1}/{totalTerritories}: checking Aether Currents in {activeTerritory.Name}",
                $"エリア {completedTerritories + 1}/{totalTerritories}: {activeTerritory.Name} の風脈を確認中"));
        }

        List<FieldCurrent> pendingFields = includeFields
            ? activeTerritory.FieldCurrents
                .Where(x => (!singleFieldId.HasValue || x.CurrentId == singleFieldId.Value) &&
                            !blockedCurrents.Contains(x.CurrentId) && !data.IsCurrentUnlocked(x.CurrentId))
                .ToList()
            : [];
        List<ushort> pendingQuests = [];
        if (includeQuests)
        {
            foreach (ushort id in activeTerritory.QuestIds.Distinct())
            {
                if (singleQuestId.HasValue && id != singleQuestId.Value)
                    continue;
                if (blockedQuests.Contains(id) || data.IsQuestComplete(id))
                    continue;
                if (questionable.IsLocked(id) && !questionable.IsAccepted(id))
                {
                    blockedQuests.Add(id);
                    LastIssue = $"{data.GetQuestName(id)}: {questionable.GetLockedReason(id)}";
                    continue;
                }
                pendingQuests.Add(id);
            }
        }

        IGameObject? player = objects.LocalPlayer;
        bool inActiveTerritory = player != null && clientState.TerritoryType == activeTerritory.TerritoryId;
        var nearbyTargets = new List<(float DistanceSquared, FieldCurrent? Field, ushort? Quest)>();
        foreach (FieldCurrent field in pendingFields)
        {
            float distance = inActiveTerritory
                ? EstimateTravelScore(player!.Position, field.Position, activeTerritory.TerritoryId)
                : 0f; // 別エリアからは従来どおりフィールド対象を先に選び、通常テレポする。
            nearbyTargets.Add((distance, field, null));
        }
        foreach (ushort id in pendingQuests)
        {
            // 受注済みは現在の進行地点が発行NPCと異なるため、Questionableでの再開を優先する。
            Vector3? questStart = data.GetQuestStartPosition(id, activeTerritory.TerritoryId);
            float distance = questionable.IsAccepted(id)
                ? 0f
                : inActiveTerritory && questStart.HasValue
                    ? EstimateTravelScore(player!.Position, questStart.Value, activeTerritory.TerritoryId)
                    : float.PositiveInfinity;
            nearbyTargets.Add((distance, null, id));
        }

        foreach (var nearby in nearbyTargets.OrderBy(x => x.DistanceSquared))
        {
            if (nearby.Field != null)
            {
                current = nearby.Field;
                questAwaitingStart = null;
                PrepareNewTravelTarget();
                SetPhase(AutomationPhase.Waiting, L.T(
                    $"Nearest target selected: preparing field Aether Current {current.CurrentId}",
                    $"近い対象を選択: フィールド風脈 {current.CurrentId} を準備中"));
                return;
            }

            ushort id = nearby.Quest!.Value;
            Vector3? questStart = data.GetQuestStartPosition(id, activeTerritory.TerritoryId);
            if (!questionable.IsAccepted(id) && questStart.HasValue)
            {
                questAwaitingStart = id;
                // CurrentId/DataId=0 はクエスト発行地点への移動専用ターゲット。
                current = new(activeTerritory.TerritoryId, 0, 0, questStart.Value);
                PrepareNewTravelTarget();
                SetPhase(AutomationPhase.Waiting, L.T(
                    $"Nearest target selected: preparing travel to the issuer of “{data.GetQuestName(id)}”",
                    $"近い対象を選択: 「{data.GetQuestName(id)}」の受注地点へ移動準備中"));
                return;
            }

            if (StartQuestionableQuest(id))
            {
                return;
            }
            blockedQuests.Add(id);
            LastIssue = L.T($"Questionable refused to start “{data.GetQuestName(id)}”.",
                $"Questionableが「{data.GetQuestName(id)}」の開始を拒否しました。");
        }

        string completedName = activeTerritory.Name;
        activeTerritory = null;
        completedTerritories++;
        SetPhase(AutomationPhase.Selecting, L.T(
            $"Finished {completedName}. Moving to the next area ({completedTerritories}/{totalTerritories})",
            $"{completedName} の処理が完了しました。次のエリアへ進みます（{completedTerritories}/{totalTerritories}）"));
    }

    private void TickQuest()
    {
        ushort id = questId!.Value;
        if (questionable.IsRunning)
        {
            SetPhase(AutomationPhase.RunningQuest, L.T($"Running “{data.GetQuestName(id)}” with Questionable",
                $"Questionableで「{data.GetQuestName(id)}」を進行中"));
            return;
        }

        ownsQuestionable = false;
        questId = null;
        if (!data.IsQuestComplete(id))
        {
            blockedQuests.Add(id);
            LastIssue = L.T($"“{data.GetQuestName(id)}” stopped before completion.",
                $"「{data.GetQuestName(id)}」は完了前に停止しました。");
        }
        SetPhase(AutomationPhase.Selecting, L.T("Selecting the next target", "次の対象を選択中"));
    }

    private bool StartQuestionableQuest(ushort id)
    {
        if (!questionable.StartSingle(id))
            return false;
        questId = id;
        ownsQuestionable = true;
        SetPhase(AutomationPhase.RunningQuest, L.T($"Running “{data.GetQuestName(id)}” with Questionable",
            $"Questionableで「{data.GetQuestName(id)}」を進行中"));
        return true;
    }

    private unsafe void TickFieldCurrent()
    {
        FieldCurrent target = current!;
        bool questPreposition = questAwaitingStart.HasValue;
        if (!questPreposition && data.IsCurrentUnlocked(target.CurrentId))
        {
            StopOwnedMovement();
            current = null;
            SetPhase(AutomationPhase.Selecting, L.T("Selecting the next target", "次の対象を選択中"));
            return;
        }

        if (!targetTeleportHandled)
        {
            StopOwnedMovement();
            if (DateTime.UtcNow < nextActionAt)
                return;

            if (aetheryteCandidates.Count == 0)
                aetheryteCandidates = data.FindUnlockedAetherytesByDistance(target.TerritoryId, target.Position);

            if (clientState.TerritoryType == target.TerritoryId && !departureSelectionComplete)
            {
                if (departurePlanTask == null)
                {
                    departurePlanTask = StartDeparturePlanning(objects.LocalPlayer!.Position, target.Position,
                        aetheryteCandidates, conditions[ConditionFlag.Swimming]);
                    Status = L.T(
                        $"Comparing ground-route distances from the current location and all {aetheryteCandidates.Count} aetheryte candidates",
                        $"現在地点と全エーテライト候補の地上経路距離を比較中（全{aetheryteCandidates.Count}候補）");
                    return;
                }
                if (!departurePlanTask.IsCompleted)
                {
                    Status = L.T(
                        $"Comparing ground-route distances from the current location and all {aetheryteCandidates.Count} aetheryte candidates",
                        $"現在地点と全エーテライト候補の地上経路距離を比較中（全{aetheryteCandidates.Count}候補）");
                    return;
                }

                DeparturePlan? plan;
                try { plan = departurePlanTask.GetAwaiter().GetResult(); }
                catch { plan = null; }
                departurePlanTask = null;
                if (plan == null)
                {
                    if (questPreposition)
                    {
                        StartQuestAfterTravelFallback(L.T(
                            "Handing travel to Questionable because a ground route or NPC transport is required",
                            "徒歩経路またはNPC移動が必要なためQuestionableへ引き渡します"));
                        return;
                    }
                    BlockCurrent(target, L.T(
                        "The target is unreachable without flying from the current location or any aetheryte candidate",
                        "現在地点および全エーテライト候補から飛行なしで到達できません"));
                    return;
                }

                departureSelectionComplete = true;
                if (plan.UseCurrentPosition)
                {
                    aetheryteCandidateIndex = -1;
                    preparedGroundPath = plan.Route.Waypoints;
                    targetTeleportHandled = true;
                    Status = L.T($"Using the ground route from the current location ({plan.Route.Length:F0}m, no flying)",
                        $"現在地点からの地上経路を採用（{plan.Route.Length:F0}m、飛行なし）");
                    return;
                }

                aetheryteCandidateIndex = plan.AetheryteIndex;
                forcedTeleportDestination = aetheryteCandidates[plan.AetheryteIndex];
                Status = L.T(
                    $"Using a reachable ground route from “{forcedTeleportDestination.Name}” ({plan.Route.Length:F0}m, candidate {plan.AetheryteIndex + 1}/{aetheryteCandidates.Count})",
                    $"到達可能な「{forcedTeleportDestination.Name}」からの地上経路を採用（{plan.Route.Length:F0}m、候補 {plan.AetheryteIndex + 1}/{aetheryteCandidates.Count}）");
            }
            TeleportDestination? destination = forcedTeleportDestination
                                               ?? aetheryteCandidates.ElementAtOrDefault(aetheryteCandidateIndex);
            if (destination == null)
            {
                if (clientState.TerritoryType != target.TerritoryId)
                {
                    LastIssue = L.T($"Could not obtain normal aetheryte coordinates for territory {target.TerritoryId}.",
                        $"対象エリア {target.TerritoryId} の通常エーテライト座標を取得できませんでした。");
                    Stop(L.T("Stopped because aetheryte coordinates could not be obtained", "エーテライト座標を取得できないため停止しました"));
                }
                else
                {
                    targetTeleportHandled = true;
                    Status = L.T("Already in the target area; traveling from the current location", "同一エリアにいるため現在地から向かいます");
                }
                return;
            }

            if (clientState.TerritoryType == target.TerritoryId)
            {
                Vector3 positionBeforeTeleport = objects.LocalPlayer!.Position;
                bool alreadyAtNearest = HorizontalDistance(positionBeforeTeleport, destination.Position) <= 35f;
                if (alreadyAtNearest)
                {
                    targetTeleportHandled = true;
                    Status = L.T($"Traveling from the nearest aetheryte, “{destination.Name}”",
                        $"最寄りの「{destination.Name}」付近から向かいます");
                    return;
                }
            }

            if (ActionManager.Instance()->GetActionStatus(ActionType.Action, 5) != 0)
            {
                Status = L.T("Waiting for Teleport to become available", "テレポの再使用待ちです");
                nextActionAt = DateTime.UtcNow.AddSeconds(1);
                return;
            }

            // ゲーム標準のテレポ処理。座標変更や不正ワープは使用しない。
            if (Telepo.Instance()->Teleport(destination.AetheryteId, destination.SubIndex))
            {
                var destinationKey = (destination.AetheryteId, destination.SubIndex);
                attemptedAetherytesForTarget.Add(destinationKey);
                lastSuccessfulTeleport = destinationKey;
                targetTeleportHandled = true;
                forcedTeleportDestination = null;
                pendingTeleportArrival = destination;
                teleportArrivalDeadline = DateTime.UtcNow.AddSeconds(45);
                // 同一エリアテレポではTerritoryIdが変化しない。詠唱・ロードが落ち着く前に
                // マウントルーレットを実行しないよう、最低待機時間も設ける。
                teleportMovementReleaseAt = DateTime.UtcNow.AddSeconds(10);
                SetPhase(AutomationPhase.Teleporting, L.T(
                    $"Candidate {aetheryteCandidateIndex + 1}/{aetheryteCandidates.Count}: teleporting to “{destination.Name}”",
                    $"候補 {aetheryteCandidateIndex + 1}/{aetheryteCandidates.Count}: 「{destination.Name}」へ通常テレポ中"));
                nextActionAt = teleportMovementReleaseAt;
            }
            else
            {
                LastIssue = L.T($"Failed to start Teleport to “{destination.Name}”.", $"「{destination.Name}」への通常テレポ開始に失敗しました。");
                Stop(L.T("Stopped because Teleport failed", "通常テレポに失敗したため停止しました"));
            }
            return;
        }

        if (pendingTeleportArrival != null)
        {
            if (DateTime.UtcNow >= teleportArrivalDeadline)
            {
                LastIssue = L.T(
                    $"Teleport to “{pendingTeleportArrival.Name}” started, but arrival could not be confirmed by coordinates within 45 seconds.",
                    $"「{pendingTeleportArrival.Name}」への通常テレポを開始しましたが、45秒以内に到着を座標確認できませんでした。");
                Stop(L.T("Stopped because Teleport did not complete", "通常テレポが完了しなかったため停止しました"));
                return;
            }

            bool inDestinationTerritory = clientState.TerritoryType == target.TerritoryId;
            float arrivalDistance = inDestinationTerritory
                ? HorizontalDistance(objects.LocalPlayer!.Position, pendingTeleportArrival.Position)
                : float.MaxValue;
            bool settled = DateTime.UtcNow >= teleportMovementReleaseAt;
            if (!inDestinationTerritory || arrivalDistance > 35f || !settled)
            {
                Status = settled
                    ? L.T($"Waiting to confirm Teleport arrival ({arrivalDistance:F0}m from aetheryte)", $"通常テレポの到着確認待ち（エーテライトまで {arrivalDistance:F0}m）")
                    : L.T("Waiting for Teleport cast/loading to finish (mount actions suppressed)", "通常テレポの詠唱・ロード完了待ち（マウント操作を抑止中）");
                return;
            }

            log.Information("Teleport arrival confirmed at {Aetheryte} ({Distance:F1}m); movement controls released",
                pendingTeleportArrival.Name, arrivalDistance);
            pendingTeleportArrival = null;
            teleportArrivalDeadline = default;
            teleportMovementReleaseAt = default;
            // テレポ前の地点で騎乗不可でも、到着先では改めて判定する。
            ResetMountAttempt();
            nextActionAt = DateTime.UtcNow.AddMilliseconds(500);
        }


        if (clientState.TerritoryType != target.TerritoryId)
        {
            if (teleportArrivalDeadline != default && DateTime.UtcNow >= teleportArrivalDeadline)
            {
                LastIssue = L.T("Teleport started, but the target area was not reached within 45 seconds.", "通常テレポを開始しましたが、45秒以内に対象エリアへ到着しませんでした。");
                Stop(L.T("Stopped because Teleport did not complete", "通常テレポが完了しなかったため停止しました"));
                return;
            }
            Status = L.T("Waiting for area transition by Teleport", "通常テレポによるエリア移動を待っています");
            return;
        }

        // 別エリアから最寄り候補へ先行テレポした場合は、対象エリアのナビメッシュが
        // 読み込まれた後に、実際の到着地点と全候補を改めて比較する。
        if (!departureSelectionComplete)
        {
            targetTeleportHandled = false;
            nextActionAt = DateTime.UtcNow;
            return;
        }

        Vector3 playerPosition = objects.LocalPlayer!.Position;
        float distance = Vector3.Distance(playerPosition, target.Position);
        if (distance > 4.2f)
        {
            if (!vnav.Ready)
            {
                Status = L.T("Waiting for the vnavmesh navigation mesh", "vnavmeshのナビメッシュ準備待ちです");
                return;
            }

            TryMountWithoutStoppingMovement();

            if (conditions[ConditionFlag.Swimming] &&
                ShouldSwitchFromSwimmingRoute(playerPosition, target) &&
                TryMoveToNextAetheryte(target, L.T("Switching to a shorter aetheryte route instead of continuing to swim", "水泳を続けるより短いエーテライト経路へ切り替えます")))
                return;

            if (recoveryWaypoint.HasValue && Vector3.Distance(playerPosition, recoveryWaypoint.Value) <= 2.5f)
            {
                StopOwnedMovement();
                recoveryWaypoint = null;
                nextActionAt = DateTime.UtcNow.AddMilliseconds(300);
                Status = L.T("Reached the detour waypoint; recalculating the route to the Aether Current", "迂回点に到達しました。風脈への経路を探索し直します");
                return;
            }

            UpdateStuckDetection(playerPosition);
            if (IsPossiblyStuck)
            {
                if (TryMoveToNextAetheryte(target))
                    return;
                LastIssue = L.T(
                    $"{activeTerritory?.Name ?? target.TerritoryId.ToString()}: stuck on routes from every aetheryte in the map.",
                    $"{activeTerritory?.Name ?? target.TerritoryId.ToString()}: マップ内のすべてのエーテライト出発経路でスタックしました。");
                Stop(L.T("Stopped after getting stuck on routes from every aetheryte", "全エーテライトからの経路でスタックしたため停止しました"));
                return;
            }

            if (phaseStarted != default && DateTime.UtcNow - phaseStarted > TimeSpan.FromMinutes(5))
            {
                BlockCurrent(target, L.T("Could not reach the target within five minutes", "5分以内に到達できませんでした"));
                return;
            }

            if (!vnav.Busy && DateTime.UtcNow >= nextActionAt)
            {
                Vector3 navigationDestination = recoveryWaypoint ?? target.Position;
                if (preparedGroundPath != null)
                {
                    IReadOnlyList<Vector3> route = preparedGroundPath;
                    preparedGroundPath = null;
                    if (vnav.FollowGroundPath(route))
                    {
                        ownsMovement = true;
                        lastMovementProgressAt = DateTime.UtcNow;
                        lastMovementProgressPosition = playerPosition;
                        SetPhase(AutomationPhase.Moving, L.T(
                            $"Following a verified ground route to the Aether Current ({distance:F0}m remaining, no flying)",
                            $"風脈まで検証済み地上経路で移動中（残り {distance:F0}m、飛行なし）"));
                        nextActionAt = DateTime.UtcNow.AddSeconds(2);
                        return;
                    }
                }
                if (pendingGroundPath == null || Vector3.DistanceSquared(pendingGroundDestination, navigationDestination) > 1f)
                {
                    pendingGroundDestination = navigationDestination;
                    pendingGroundPath = vnav.FindGroundPath(playerPosition, navigationDestination);
                    Status = L.T("Calculating a non-flying ground route", "飛行なしの徒歩経路を計算中");
                    nextActionAt = DateTime.UtcNow.AddMilliseconds(250);
                    return;
                }
                if (!pendingGroundPath.IsCompleted)
                {
                    Status = L.T("Calculating a non-flying ground route", "飛行なしの徒歩経路を計算中");
                    return;
                }

                IReadOnlyList<Vector3>? groundPath;
                try { groundPath = pendingGroundPath.GetAwaiter().GetResult(); }
                catch { groundPath = null; }
                pendingGroundPath = null;
                bool reachesTarget = groundPath is { Count: > 0 } &&
                                     Vector3.Distance(groundPath[^1], navigationDestination) <= 8f;
                if (reachesTarget && vnav.FollowGroundPath(groundPath!))
                {
                    ownsMovement = true;
                    lastMovementProgressAt = DateTime.UtcNow;
                    lastMovementProgressPosition = playerPosition;
                    string route = recoveryWaypoint.HasValue
                        ? L.T("ground detour", "地上迂回経路")
                        : L.T("walking/ground-mount route", "徒歩・地上マウント経路");
                    SetPhase(AutomationPhase.Moving, L.T(
                        $"Following the {route} to the Aether Current ({distance:F0}m remaining, no flying)",
                        $"風脈まで{route}で移動中（残り {distance:F0}m、飛行なし）"));
                }
                else
                {
                    Status = L.T("No reachable ground route found; trying another aetheryte candidate", "到達可能な徒歩経路が見つかりません。別のエーテライト候補を試します");
                    if (TryMoveToNextAetheryte(target))
                        return;
                    BlockCurrent(target, L.T("No reachable ground route was found from any aetheryte candidate", "全エーテライト候補から到達可能な徒歩経路が見つかりませんでした"));
                    return;
                }
                nextActionAt = DateTime.UtcNow.AddSeconds(2);
            }
            else
            {
                string warning = IsPossiblyStuck
                    ? L.T(" — possibly stuck; recalculate the route", " — スタックの可能性あり。通路を再計算してください")
                    : string.Empty;
                Status = L.T($"Traveling normally to the Aether Current ({distance:F0}m remaining){warning}",
                    $"風脈まで正規移動中（残り {distance:F0}m）{warning}");
            }
            return;
        }

        StopOwnedMovement();
        if (DateTime.UtcNow < nextActionAt)
            return;

        if (questPreposition)
        {
            ushort id = questAwaitingStart!.Value;
            current = null;
            questAwaitingStart = null;
            ResetAetheryteRouting();
            ResetPathRecovery();
            if (!StartQuestionableQuest(id))
            {
                blockedQuests.Add(id);
                LastIssue = L.T($"Questionable refused to start “{data.GetQuestName(id)}”.",
                    $"Questionableが「{data.GetQuestName(id)}」の開始を拒否しました。");
                SetPhase(AutomationPhase.Selecting, L.T("Selecting the next target", "次の対象を選択中"));
            }
            return;
        }

        if (Phase is not AutomationPhase.Waiting and not AutomationPhase.Interacting)
            SetPhase(AutomationPhase.Waiting, L.T("Looking for the Aether Current object", "風脈オブジェクトを確認中"));

        IGameObject? obj = objects.FirstOrDefault(x => x.BaseId == target.DataId && x.ObjectKind == ObjectKind.EventObj);
        if (obj == null || !obj.IsTargetable)
        {
            if (DateTime.UtcNow - phaseStarted > TimeSpan.FromSeconds(20))
                BlockCurrent(target, L.T("Arrived, but the Aether Current object could not be found", "到着しましたが風脈オブジェクトを確認できませんでした"));
            else
                Status = L.T("Waiting for the Aether Current object to load", "風脈オブジェクトの読み込み待ちです");
            nextActionAt = DateTime.UtcNow.AddSeconds(1);
            return;
        }

        targets.Target = obj;
        long result = (long)TargetSystem.Instance()->InteractWithObject((GameObjectStruct*)obj.Address, checkLineOfSight: true);
        log.Information("Interacted with aether current {CurrentId}/{DataId}, result {Result}", target.CurrentId, target.DataId, result);
        SetPhase(AutomationPhase.Interacting, L.T("Attuning to the Aether Current", "風脈に交感しています"));
        nextActionAt = DateTime.UtcNow.AddSeconds(2);
    }

    private void BlockCurrent(FieldCurrent target, string reason)
    {
        if (questAwaitingStart.HasValue)
        {
            StartQuestAfterTravelFallback(reason + L.T(". Handing travel to Questionable", "。移動をQuestionableへ引き渡します"));
            return;
        }
        StopOwnedMovement();
        blockedCurrents.Add(target.CurrentId);
        LastIssue = L.T($"Aether Current {target.CurrentId} (territory {target.TerritoryId}): {reason}",
            $"風脈 {target.CurrentId}（エリア {target.TerritoryId}）: {reason}");
        current = null;
        SetPhase(AutomationPhase.Selecting, L.T("Selecting the next target", "次の対象を選択中"));
    }

    private void StartQuestAfterTravelFallback(string reason)
    {
        ushort id = questAwaitingStart!.Value;
        StopOwnedMovement();
        current = null;
        questAwaitingStart = null;
        ResetAetheryteRouting();
        ResetPathRecovery();
        LastIssue = L.T($"“{data.GetQuestName(id)}”: {reason}", $"「{data.GetQuestName(id)}」: {reason}");
        if (!StartQuestionableQuest(id))
        {
            blockedQuests.Add(id);
            LastIssue += L.T(". Questionable refused to start it.", "。Questionableが開始を拒否しました。");
            SetPhase(AutomationPhase.Selecting, L.T("Selecting the next target", "次の対象を選択中"));
        }
    }

    private void PauseMovement(string status)
    {
        StopOwnedMovement();
        Status = status;
    }

    private void StopOwnedMovement()
    {
        if (ownsMovement)
            vnav.Stop();
        ownsMovement = false;
        pendingGroundPath = null;
        preparedGroundPath = null;
    }

    public bool RecalculatePath()
    {
        if (!CanRecalculatePath || objects.LocalPlayer == null || current == null)
            return false;

        // Path.Stopで現在の追従経路を破棄する。SimpleMoveの非同期探索が終了した後、次Tickで新規探索する。
        vnav.Stop();
        ownsMovement = false;
        pendingGroundPath = null;
        preparedGroundPath = null;
        pathRecalculationCount++;

        Vector3 player = objects.LocalPlayer.Position;
        // ボタンを押した地点を新しい始点にする。人工的な迂回点は挟まず、
        // 現在位置から風脈までの地上経路をvnavmeshに完全に引き直させる。
        recoveryWaypoint = null;

        lastMovementProgressAt = DateTime.UtcNow;
        lastMovementProgressPosition = player;
        IsPossiblyStuck = false;
        nextActionAt = DateTime.UtcNow.AddMilliseconds(500);
        SetPhase(AutomationPhase.Waiting, L.T(
            $"Recalculating the route from the current location (attempt {pathRecalculationCount})",
            $"現在地点から通路を再計算します（{pathRecalculationCount}回目）"));
        log.Information("Recalculating ground path from {Player} to aether current {CurrentId}; attempt {Attempt}",
            player, current.CurrentId, pathRecalculationCount);
        return true;
    }

    private Task<DeparturePlan?> StartDeparturePlanning(
        Vector3 currentPosition, Vector3 targetPosition, IReadOnlyList<TeleportDestination> candidates,
        bool currentOriginSwimming)
    {
        // 全IPC呼び出しはTickのメインスレッド上で開始し、完了待ちだけを非同期化する。
        Task<IReadOnlyList<Vector3>?> currentTask = vnav.FindGroundPath(currentPosition, targetPosition);
        Task<IReadOnlyList<Vector3>?>[] candidateTasks = candidates
            .Select(x => vnav.FindGroundPath(x.Position, targetPosition))
            .ToArray();
        return CompleteDeparturePlanning(currentPosition, targetPosition, candidates, currentOriginSwimming,
            currentTask, candidateTasks);
    }

    private async Task<DeparturePlan?> CompleteDeparturePlanning(
        Vector3 currentPosition,
        Vector3 targetPosition,
        IReadOnlyList<TeleportDestination> candidates,
        bool currentOriginSwimming,
        Task<IReadOnlyList<Vector3>?> currentTask,
        Task<IReadOnlyList<Vector3>?>[] candidateTasks)
    {
        IReadOnlyList<Vector3>? currentPath = await currentTask.ConfigureAwait(false);
        IReadOnlyList<Vector3>?[] candidatePaths = await Task.WhenAll(candidateTasks).ConfigureAwait(false);
        GroundRoute? currentRoute = ValidateGroundRoute(currentPath, currentPosition, targetPosition);

        if (candidates.Count == 0)
            return currentRoute == null ? null : new(true, -1, currentRoute);

        DeparturePlan? best = currentRoute == null ? null : new(true, -1, currentRoute);
        DeparturePlan? bestAetherytePlan = null;
        for (int i = 0; i < candidates.Count; i++)
        {
            GroundRoute? candidateRoute = ValidateGroundRoute(candidatePaths[i], candidates[i].Position, targetPosition);
            log.Information(
                "Ground reachability for target from candidate {Index}/{Count} {Aetheryte}: current={Current}, candidate={Candidate}",
                i + 1, candidates.Count, candidates[i].Name,
                currentRoute == null ? "unreachable" : $"{currentRoute.Length:F1}m",
                candidateRoute == null ? "unreachable" : $"{candidateRoute.Length:F1}m");

            // 直線距離ではなく、障害物や迂回を含んだ徒歩経路の実距離で比較する。
            // 次の対象へ切り替わるたびに現在地点と全候補を再計算するため、前の風脈・
            // クエスト終了地点から歩き続けるより短い候補があれば通常テレポを選ぶ。
            if (candidateRoute != null &&
                (bestAetherytePlan == null || candidateRoute.Length < bestAetherytePlan.Route.Length))
                bestAetherytePlan = new(false, i, candidateRoute);
        }

        if (bestAetherytePlan != null)
        {
            float currentScore = currentRoute?.Length ?? float.PositiveInfinity;
            if (currentOriginSwimming)
                currentScore *= 2.2f;

            TeleportDestination candidate = candidates[bestAetherytePlan.AetheryteIndex];
            float currentHorizontal = HorizontalDistance(currentPosition, targetPosition);
            float candidateHorizontal = HorizontalDistance(candidate.Position, targetPosition);
            bool clearlyCloserByAetheryte = candidateHorizontal + 100f < currentHorizontal &&
                                            bestAetherytePlan.Route.Length <= currentScore * 1.75f;
            if (best == null || bestAetherytePlan.Route.Length < currentScore || clearlyCloserByAetheryte)
                best = bestAetherytePlan;
        }

        if (best != null)
        {
            log.Information(
                "Selected shortest ground route for target: origin={Origin}, length={Length:F1}m",
                best.UseCurrentPosition ? "current position" : candidates[best.AetheryteIndex].Name,
                best.Route.Length);
        }
        return best;
    }

    private static GroundRoute? ValidateGroundRoute(
        IReadOnlyList<Vector3>? path, Vector3 start, Vector3 target)
    {
        if (path == null || path.Count == 0 || Vector3.Distance(path[^1], target) > 8f)
            return null;

        // 最初の点はvnavmeshがゲームデータ座標を地上メッシュへスナップした位置。
        // エーテライト結晶のLevel座標とのY差を飛行区間と誤判定しないよう、検査はここから始める。
        float length = Vector3.Distance(start, path[0]);
        Vector3 previous = path[0];
        for (int i = 1; i < path.Count; i++)
        {
            Vector3 point = path[i];
            float dx = point.X - previous.X;
            float dz = point.Z - previous.Z;
            float horizontal = MathF.Sqrt(dx * dx + dz * dz);
            float vertical = MathF.Abs(point.Y - previous.Y);

            // vnavmeshが地上経路として返しても、徒歩では越えられない急な垂直区間を
            // 含む場合は飛行前提の経路とみなして除外する。
            if ((horizontal < 0.5f && vertical > 2.5f) ||
                (vertical > 4f && vertical / MathF.Max(horizontal, 0.1f) > 1.1f))
                return null;

            length += Vector3.Distance(previous, point);
            previous = point;
        }
        return new(path, length);
    }

    private static float HorizontalDistance(Vector3 from, Vector3 to)
    {
        float dx = from.X - to.X;
        float dz = from.Z - to.Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }

    private float EstimateTravelScore(Vector3 currentPosition, Vector3 targetPosition, uint territoryId)
    {
        float currentScore = HorizontalDistance(currentPosition, targetPosition);
        if (conditions[ConditionFlag.Swimming])
            currentScore *= 2.2f;

        IReadOnlyList<TeleportDestination> candidates =
            data.FindUnlockedAetherytesByDistance(territoryId, targetPosition);
        float teleportScore = candidates.Count == 0
            ? float.PositiveInfinity
            : HorizontalDistance(candidates[0].Position, targetPosition) + 100f;
        return MathF.Min(currentScore, teleportScore);
    }

    private void UpdateStuckDetection(Vector3 playerPosition)
    {
        DateTime now = DateTime.UtcNow;
        if (lastMovementProgressAt == default)
        {
            lastMovementProgressAt = now;
            lastMovementProgressPosition = playerPosition;
            IsPossiblyStuck = false;
            return;
        }

        if (Vector3.DistanceSquared(lastMovementProgressPosition, playerPosition) >= 4f)
        {
            lastMovementProgressAt = now;
            lastMovementProgressPosition = playerPosition;
            IsPossiblyStuck = false;
            return;
        }

        IsPossiblyStuck = now - lastMovementProgressAt >= TimeSpan.FromSeconds(12);
    }

    private void ResetPathRecovery()
    {
        recoveryWaypoint = null;
        pendingGroundPath = null;
        preparedGroundPath = null;
        pathRecalculationCount = 0;
        lastMovementProgressAt = default;
        lastMovementProgressPosition = default;
        IsPossiblyStuck = false;
    }

    /// <summary>
    /// 連続処理でも単品開始と同じ初期状態から、現在地と全エーテライト候補を評価する。
    /// </summary>
    private void PrepareNewTravelTarget()
    {
        StopOwnedMovement();
        targetTeleportHandled = false;
        teleportArrivalDeadline = default;
        teleportMovementReleaseAt = default;
        pendingTeleportArrival = null;
        nextActionAt = DateTime.UtcNow;
        ResetAetheryteRouting();
        ResetPathRecovery();
        ResetMountAttempt();
    }

    private bool TryMoveToNextAetheryte(FieldCurrent target, string? reason = null)
    {
        if (aetheryteCandidates.Count == 0)
            aetheryteCandidates = data.FindUnlockedAetherytesByDistance(target.TerritoryId, target.Position);

        int nextIndex = -1;
        for (int i = 0; i < aetheryteCandidates.Count; i++)
        {
            TeleportDestination candidate = aetheryteCandidates[i];
            var key = (candidate.AetheryteId, candidate.SubIndex);
            if (attemptedAetherytesForTarget.Contains(key) || lastSuccessfulTeleport == key)
                continue;
            nextIndex = i;
            break;
        }
        if (nextIndex < 0)
            return false;

        vnav.Stop();
        ownsMovement = false;
        pendingGroundPath = null;
        preparedGroundPath = null;
        departurePlanTask = null;
        departureSelectionComplete = true;
        aetheryteCandidateIndex = nextIndex;
        forcedTeleportDestination = aetheryteCandidates[nextIndex];
        targetTeleportHandled = false;
        teleportArrivalDeadline = default;
        nextActionAt = DateTime.UtcNow;
        ResetMountAttempt();
        ResetPathRecovery();
        string fallbackReason = reason ?? L.T(
            "Stuck detected; switching to an unused aetheryte",
            "スタックを検知したため未使用のエーテライトへ切り替えます");
        Status = L.T(
            $"{fallbackReason}: retrying from “{forcedTeleportDestination.Name}” ({nextIndex + 1}/{aetheryteCandidates.Count})",
            $"{fallbackReason}: 「{forcedTeleportDestination.Name}」から再試行（{nextIndex + 1}/{aetheryteCandidates.Count}）");
        log.Warning("Stuck while moving to aether current {CurrentId}; retrying from aetheryte {AetheryteId} ({Index}/{Count})",
            target.CurrentId, forcedTeleportDestination.AetheryteId, nextIndex + 1, aetheryteCandidates.Count);
        return true;
    }

    private bool ShouldSwitchFromSwimmingRoute(Vector3 playerPosition, FieldCurrent target)
    {
        float swimmingScore = HorizontalDistance(playerPosition, target.Position) * 2.2f;
        foreach (TeleportDestination candidate in aetheryteCandidates)
        {
            var key = (candidate.AetheryteId, candidate.SubIndex);
            if (attemptedAetherytesForTarget.Contains(key) || lastSuccessfulTeleport == key)
                continue;

            // Approximate teleport/cast overhead as 100m of normal travel. Switch only when
            // the remaining swim is clearly slower, avoiding needless teleports near the target.
            float teleportScore = HorizontalDistance(candidate.Position, target.Position) + 100f;
            if (teleportScore + 40f < swimmingScore)
                return true;
        }
        return false;
    }

    private void ResetAetheryteRouting()
    {
        aetheryteCandidates = [];
        forcedTeleportDestination = null;
        pendingTeleportArrival = null;
        aetheryteCandidateIndex = 0;
        departurePlanTask = null;
        preparedGroundPath = null;
        departureSelectionComplete = false;
        attemptedAetherytesForTarget.Clear();
        teleportArrivalDeadline = default;
        teleportMovementReleaseAt = default;
    }

    private unsafe void TryMountWithoutStoppingMovement()
    {
        if (!data.CanUseMount(clientState.TerritoryType))
            return;
        if (conditions[ConditionFlag.Mounted])
        {
            wasMounted = true;
            mountAttemptCount = 0;
            nextMountAttemptAt = default;
            mountWasTemporarilyBlocked = false;
            return;
        }

        if (wasMounted)
        {
            // Swimming can dismount the player. Leaving the water starts a fresh retry budget.
            wasMounted = false;
            mountAttemptCount = 0;
            nextMountAttemptAt = default;
        }

        if (HasStatusPreventingMount())
        {
            mountWasTemporarilyBlocked = true;
            nextMountAttemptAt = DateTime.UtcNow.AddSeconds(1);
            return;
        }

        if (mountWasTemporarilyBlocked)
        {
            mountWasTemporarilyBlocked = false;
            mountAttemptCount = 0;
            nextMountAttemptAt = default;
        }

        DateTime now = DateTime.UtcNow;

        if (conditions[ConditionFlag.Mounting] || conditions[ConditionFlag.MountOrOrnamentTransition])
            return;

        if (mountAttemptCount >= 10 || now < nextMountAttemptAt)
            return;

        ActionManager* actionManager = ActionManager.Instance();
        if (actionManager == null)
            return;

        uint mountId = selectedMountProvider();
        ActionType actionType = mountId == 0 ? ActionType.GeneralAction : ActionType.Mount;
        uint actionId = mountId == 0 ? 9u : mountId;
        string mountName = mountId == 0
            ? L.T("Mount Roulette", "マウント・ルーレット")
            : L.T($"selected mount (ID {mountId})", $"指定マウント（ID {mountId}）");
        mountAttemptCount++;
        uint actionStatus = actionManager->GetActionStatus(actionType, actionId);
        if (actionStatus != 0)
        {
            nextMountAttemptAt = now.AddSeconds(1);
            log.Information("Mount attempt {Attempt}/10 for {MountName} unavailable; action status={ActionStatus}",
                mountAttemptCount, mountName, actionStatus);
            return;
        }

        bool started = actionManager->UseAction(actionType, actionId);
        nextMountAttemptAt = now.AddSeconds(started ? 2 : 1);
        log.Information("Mount attempt {Attempt}/10 for {MountName}: {Started}; navigation continues",
            mountAttemptCount, mountName, started);
    }

    private void ResetMountAttempt()
    {
        nextMountAttemptAt = default;
        mountAttemptCount = 0;
        wasMounted = conditions[ConditionFlag.Mounted];
        mountWasTemporarilyBlocked = false;
    }

    private unsafe bool HasStatusPreventingMount()
    {
        // QuestionableのMountEvaluator/GameFunctionsと同じ主要条件。
        // 戦闘中は移動を継続しつつ、騎乗の再試行回数を消費しない。
        if (conditions[ConditionFlag.Swimming] || conditions[ConditionFlag.InCombat])
            return true;

        IGameObject? player = objects.LocalPlayer;
        if (player == null)
            return true;

        BattleChara* battleChara = (BattleChara*)player.Address;
        StatusManager* statusManager = battleChara->GetStatusManager();
        return statusManager->HasStatus(1151) || // Hoofing It系
               statusManager->HasStatus(1945) || // Hoofing It
               statusManager->HasStatus(565) ||  // Transfiguration
               statusManager->HasStatus(416) ||  // Transparent
               statusManager->HasStatus(404) ||  // Transporting
               statusManager->HasStatus(4376) || // Transporting
               statusManager->HasStatus(2729) || // Incorporeal
               statusManager->HasStatus(2730);   // Endwalker特殊状態
    }

    private void SetPhase(AutomationPhase phase, string status)
    {
        if (Phase != phase)
            phaseStarted = DateTime.UtcNow;
        Phase = phase;
        Status = status;
    }

    private bool Reject(string message)
    {
        LastIssue = message;
        Status = message;
        Phase = AutomationPhase.Error;
        return false;
    }
}
