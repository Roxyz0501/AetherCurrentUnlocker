using System.Numerics;
using AetherCurrentUnlocker.Automation;
using AetherCurrentUnlocker.Data;
using AetherCurrentUnlocker.Ipc;
using AetherCurrentUnlocker.Models;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Utility;

namespace AetherCurrentUnlocker.Windows;

internal sealed class MainWindow
{
    private static readonly Vector4 Green = new(0.35f, 0.9f, 0.45f, 1f);
    private static readonly Vector4 Yellow = new(1f, 0.78f, 0.25f, 1f);
    private static readonly Vector4 Red = new(1f, 0.38f, 0.38f, 1f);
    private readonly Configuration config;
    private readonly Action save;
    private readonly AetherCurrentDataService data;
    private readonly AutomationController automation;
    private readonly VNavmeshIpc vnav;
    private readonly QuestionableIpc questionable;
    private readonly IClientState clientState;
    private readonly IPlayerState playerState;
    private readonly ITextureProvider textureProvider;
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly Action languageChanged;
    private readonly Dictionary<ushort, (DateTime ExpiresAt, string Text, Vector4 Color)> questStatusCache = [];
    private readonly Dictionary<uint, (DateTime ExpiresAt, IReadOnlyList<TeleportDestination> Items)> aetheryteMapCache = [];
    private (DateTime ExpiresAt, IReadOnlyList<MountChoice> Items) mountCache;
    private bool confirmExpansion;
    private uint mapTerritoryId;

    private CharacterConfiguration Settings => config.ForCharacter(playerState.ContentId);

    public MainWindow(
        Configuration config,
        Action save,
        AetherCurrentDataService data,
        AutomationController automation,
        VNavmeshIpc vnav,
        QuestionableIpc questionable,
        IClientState clientState,
        IPlayerState playerState,
        ITextureProvider textureProvider,
        IDalamudPluginInterface pluginInterface,
        Action languageChanged)
    {
        this.config = config;
        this.save = save;
        this.data = data;
        this.automation = automation;
        this.vnav = vnav;
        this.questionable = questionable;
        this.clientState = clientState;
        this.playerState = playerState;
        this.textureProvider = textureProvider;
        this.pluginInterface = pluginInterface;
        this.languageChanged = languageChanged;
    }

    public bool IsOpen { get; set; }

    public void Draw()
    {
        if (!IsOpen)
            return;

        ImGui.SetNextWindowSize(new Vector2(980, 720), ImGuiCond.FirstUseEver);
        bool isOpen = IsOpen;
        if (!ImGui.Begin("Aether Current Navigator###AetherCurrentUnlocker", ref isOpen))
        {
            IsOpen = isOpen;
            ImGui.End();
            return;
        }
        IsOpen = isOpen;

        DrawControlPanel();
        ImGui.Separator();
        DrawProgress();
        DrawConfirmationPopup();
        ImGui.End();
    }

    private void DrawControlPanel()
    {
        string currentFieldButton = L.T("Unlock current area  ▶", "現在のフィールドを解放  ▶");
        string expansionButton = L.T("Unlock selected expansion  ▶", "指定拡張をすべて解放  ▶");
        float actionButtonWidth = MathF.Max(
            ImGui.CalcTextSize(currentFieldButton).X,
            ImGui.CalcTextSize(expansionButton).X) + ImGui.GetStyle().FramePadding.X * 2f;

        ImGui.TextColored(new Vector4(0.55f, 0.8f, 1f, 1f), "Aether Current Navigator");
        ImGui.SameLine();
        ImGui.TextDisabled($"— {automation.Status}");
        if (automation.IsRunning && automation.TotalTerritories > 0 && automation.ActiveTerritoryName != null)
            ImGui.TextUnformatted(L.T($"Processing: {automation.ActiveTerritoryName}  ({automation.CompletedTerritories + 1}/{automation.TotalTerritories})",
                $"処理中: {automation.ActiveTerritoryName}  ({automation.CompletedTerritories + 1}/{automation.TotalTerritories})"));
        if (automation.CurrentTarget != null && automation.AetheryteCandidateCount > 0)
        {
            string distance = automation.CurrentAetheryteHorizontalDistance is { } value ? $"{value:F0}m" : "—";
            ImGui.TextDisabled(L.T(
                $"Route candidate: {automation.CurrentAetheryteCandidate}/{automation.AetheryteCandidateCount}  {automation.CurrentAetheryteName}  horizontal distance {distance}",
                $"経路候補: {automation.CurrentAetheryteCandidate}/{automation.AetheryteCandidateCount}  {automation.CurrentAetheryteName}  水平距離 {distance}"));
        }
        if (!string.IsNullOrWhiteSpace(automation.LastIssue))
            ImGui.TextColored(Yellow, L.T($"Latest notice: {automation.LastIssue}", $"直近の注意: {automation.LastIssue}"));

        ImGui.BeginDisabled(automation.IsRunning);
        if (ImGui.Button(currentFieldButton, new Vector2(actionButtonWidth, 0)))
            automation.StartCurrentTerritory(Settings.IncludeFieldCurrents, Settings.IncludeQuestCurrents);
        ImGui.EndDisabled();

        ImGui.BeginDisabled(automation.IsRunning);
        if (ImGui.Button(expansionButton, new Vector2(actionButtonWidth, 0)))
        {
            if (Settings.ConfirmExpansionRun)
            {
                confirmExpansion = true;
                ImGui.OpenPopup(L.T("Confirm expansion run", "拡張単位の実行確認"));
            }
            else
                StartExpansion();
        }
        ImGui.SameLine();
        string selectedName = AetherCurrentDataService.ExpansionIds.Contains(Settings.SelectedExpansion)
            ? L.Expansion(Settings.SelectedExpansion)
            : L.T("Select an expansion", "選択してください");
        ImGui.SetNextItemWidth(220);
        if (ImGui.BeginCombo("###Expansion", selectedName))
        {
            foreach (uint id in AetherCurrentDataService.ExpansionIds)
            {
                string name = L.Expansion(id);
                if (ImGui.Selectable(name, id == Settings.SelectedExpansion))
                {
                    Settings.SelectedExpansion = id;
                    save();
                }
            }
            ImGui.EndCombo();
        }
        ImGui.EndDisabled();

        ImGui.BeginDisabled(!automation.IsRunning);
        if (DrawIconButton("Stop", FontAwesomeIcon.Stop, new Vector2(46, 0)))
            automation.Stop(L.T("Stopped by user", "ユーザー操作で停止しました"));
        ImGui.EndDisabled();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(L.T("Stop", "停止"));

        ImGui.SameLine();
        bool canTogglePause = automation.IsPaused || automation.CanPause;
        ImGui.BeginDisabled(!canTogglePause);
        if (DrawIconButton(automation.IsPaused ? "Resume" : "Pause",
                automation.IsPaused ? FontAwesomeIcon.Play : FontAwesomeIcon.Pause, new Vector2(46, 0)))
        {
            if (automation.IsPaused)
                automation.Resume();
            else
                automation.Pause();
        }
        ImGui.EndDisabled();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(automation.IsPaused ? L.T("Resume", "再開") : automation.CanPause
                ? L.T("Pause", "一時停止")
                : L.T("Cannot pause while teleporting or while Questionable is running", "テレポ中・Questionable実行中は一時停止できません"));

        ImGui.SameLine();
        ImGui.BeginDisabled(!automation.CanRecalculatePath);
        if (ImGui.Button(L.T("Recalculate route", "通路を再計算")))
            automation.RecalculatePath();
        ImGui.EndDisabled();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(automation.CanRecalculatePath
                ? L.T("Discard the current route and calculate a new ground route from your present location.", "現在の経路を破棄し、ボタンを押した現在地点から徒歩経路を引き直します。")
                : L.T("Available while traveling to a field Aether Current.", "フィールド風脈への移動中に使用できます。"));

        if (automation.IsPossiblyStuck)
            ImGui.TextColored(Red, automation.HasNextAetheryteCandidate
                ? L.T("Stuck detected. Automatically switching to an unused aetheryte.", "スタックを検知しました。未使用のエーテライトへ自動で切り替えます。")
                : L.T("Automation will stop because every aetheryte departure route got stuck.", "すべてのエーテライト出発経路でスタックしたため、自動処理を停止します。"));
        if (automation.PathRecalculationCount > 0)
            ImGui.TextUnformatted(L.T($"Route recalculations for this current: {automation.PathRecalculationCount}",
                $"この風脈での通路再計算: {automation.PathRecalculationCount} 回"));
    }

    private void DrawProgress()
    {
        if (ImGui.BeginTabBar("ProgressTabs"))
        {
            if (ImGui.BeginTabItem(L.T("All expansions", "全拡張の進捗")))
            {
                foreach (uint expansionId in AetherCurrentDataService.ExpansionIds)
                    DrawExpansion(expansionId, L.Expansion(expansionId));
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem(L.T("Current area", "現在エリア")))
            {
                TerritoryProgress? territory = data.GetTerritory(clientState.TerritoryType);
                if (territory == null)
                    ImGui.TextUnformatted(L.T("The current area has no Aether Current data.", "現在エリアには風脈データがありません。"));
                else
                    DrawTerritory(territory, true);
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem(L.T("Aether Current map", "風脈マップ")))
            {
                DrawAetherCurrentMap();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem(L.T("Status", "状態")))
            {
                DrawPluginStatus();
                ImGui.EndTabItem();
            }

            ImGui.PushStyleColor(ImGuiCol.Tab, new Vector4(0.55f, 0.34f, 0.05f, 1f));
            ImGui.PushStyleColor(ImGuiCol.TabHovered, new Vector4(0.95f, 0.58f, 0.08f, 1f));
            ImGui.PushStyleColor(ImGuiCol.TabActive, new Vector4(0.82f, 0.47f, 0.05f, 1f));
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.95f, 0.82f, 1f));
            bool supportOpen = ImGui.BeginTabItem(L.T("Support", "支援"));
            ImGui.PopStyleColor(4);
            if (supportOpen)
            {
                DrawSupport();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem(L.T("Config", "設定")))
            {
                DrawConfig();
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }
    }

    private static void DrawSupport()
    {
        const string supportUrl = "https://ko-fi.com/roxyz0501";
        ImGui.Spacing();
        ImGui.TextColored(new Vector4(1f, 0.7f, 0.18f, 1f), L.T("Support Roxyz0501's development", "Roxyz0501の開発を支援"));
        ImGui.Spacing();
        ImGui.TextWrapped(L.T(
            "You may optionally support development of Aether Current Navigator. Every feature remains available without a contribution.",
            "Aether Current Navigatorの開発を任意で支援できます。支援しなくても、すべての機能を利用できます。"));
        ImGui.TextWrapped(L.T("Recipient: Roxyz0501", "支援先: Roxyz0501"));
        ImGui.TextDisabled(supportUrl);
        ImGui.Spacing();

        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.78f, 0.39f, 0.05f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.95f, 0.52f, 0.08f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.68f, 0.31f, 0.03f, 1f));
        bool clicked = ImGui.Button(L.T("Support Roxyz0501 on Ko-fi", "Ko-fiでRoxyz0501を支援"), new Vector2(300, 46));
        ImGui.PopStyleColor(3);
        if (clicked)
            Util.OpenLink(supportUrl);
    }

    private void DrawConfig()
    {
        DisplayLanguage language = config.DisplayLanguage ?? DisplayLanguage.English;
        string languageName = language == DisplayLanguage.Japanese ? "日本語" : "English";
        ImGui.SetNextItemWidth(180);
        if (ImGui.BeginCombo(L.T("Display language", "表示言語"), languageName))
        {
            foreach (DisplayLanguage choice in new[] { DisplayLanguage.English, DisplayLanguage.Japanese })
            {
                string choiceName = choice == DisplayLanguage.Japanese ? "日本語" : "English";
                if (!ImGui.Selectable(choiceName, language == choice))
                    continue;
                config.DisplayLanguage = choice;
                questStatusCache.Clear();
                aetheryteMapCache.Clear();
                mountCache = default;
                save();
                languageChanged();
            }
            ImGui.EndCombo();
        }
        ImGui.TextDisabled(L.T(
            "Selected automatically from the game language on first launch, then kept until you change it.",
            "初回起動時のみゲーム言語から選択し、以後は変更するまで固定されます。"));

        CharacterConfiguration settings = Settings;
        string characterName = playerState.IsLoaded ? playerState.CharacterName.ToString() : L.T("logged-in character", "ログイン中のキャラクター");
        ImGui.TextDisabled(L.T($"Per-character settings — {characterName}", $"キャラクター別設定 — {characterName}"));

        ImGui.TextUnformatted(L.T("Targets", "処理対象"));
        bool fields = settings.IncludeFieldCurrents;
        if (ImGui.Checkbox(L.T("Field Aether Currents", "フィールド風脈"), ref fields))
        {
            settings.IncludeFieldCurrents = fields;
            save();
        }
        ImGui.SameLine();
        bool quests = settings.IncludeQuestCurrents;
        if (ImGui.Checkbox(L.T("Aether Current quests", "風脈クエスト"), ref quests))
        {
            settings.IncludeQuestCurrents = quests;
            save();
        }

        bool confirm = settings.ConfirmExpansionRun;
        if (ImGui.Checkbox(L.T("Confirm before running an expansion", "拡張単位の開始前に確認する"), ref confirm))
        {
            settings.ConfirmExpansionRun = confirm;
            save();
        }

        DrawMountSelector(settings);

        bool debug = settings.ShowDebugInformation;
        if (ImGui.Checkbox(L.T("Show debug information", "デバッグ情報の表示"), ref debug))
        {
            settings.ShowDebugInformation = debug;
            save();
        }

        DrawSectionTitle(L.T("Commands", "コマンド"));
        ImGui.TextUnformatted("/acnav");
        ImGui.SameLine();
        ImGui.TextDisabled(L.T("Open the window", "ウィンドウを開く"));
        ImGui.TextUnformatted("/acnav stop");
        ImGui.SameLine();
        ImGui.TextDisabled(L.T("Stop automation", "実行を停止"));

        if (settings.ShowDebugInformation)
        {
            DrawSectionTitle(L.T("Debug", "デバッグ"));
            ImGui.TextDisabled($"Aetheryte: {data.LastAetheryteScanStatus}");
            ImGui.TextDisabled($"vnavmesh: available={vnav.Available}, ready={vnav.Ready}, busy={vnav.Busy}");
            ImGui.TextDisabled($"Automation: phase={automation.Phase}, paused={automation.IsPaused}");
        }
    }

    private void DrawPluginStatus()
    {
        ImGui.TextUnformatted(L.T("Plugin requirements", "前提プラグイン"));
        ImGui.TextDisabled(L.T("Shows whether each feature is available.", "各機能が利用できる状態かを表示します。"));
        if (!ImGui.BeginTable("DependencyDetails", 3,
                ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp))
            return;

        ImGui.TableSetupColumn(L.T("Plugin", "プラグイン"), ImGuiTableColumnFlags.WidthFixed, 170);
        ImGui.TableSetupColumn(L.T("Status", "状態"), ImGuiTableColumnFlags.WidthFixed, 130);
        ImGui.TableSetupColumn(L.T("Role", "用途"));
        ImGui.TableHeadersRow();
        DrawDependencyRow("vnavmesh", vnav.Available,
            vnav.Available ? vnav.Ready ? L.T("● Ready", "● 使用可能") : L.T("● Loaded", "● 読み込み済み") : L.T("● Not loaded", "● 未読み込み"),
            L.T("Required and used directly for field travel and Questionable movement.", "必須・直接利用。フィールド移動とQuestionableの移動処理で使用"));
        DrawDependencyRow("Questionable", questionable.Available,
            questionable.Available ? L.T("● Loaded", "● 読み込み済み") : L.T("● Not loaded", "● 未読み込み"),
            L.T("Used directly for Aether Current quests; optional for field-current-only use.", "風脈クエスト機能で直接利用。フィールド風脈のみを処理する場合は任意"));
        DrawDependencyRow("TextAdvance", IsPluginLoaded("TextAdvance"), null,
            L.T("Questionable requirement (indirect dependency).", "Questionableの前提プラグイン（間接依存）"));
        DrawDependencyRow("Lifestream", IsPluginLoaded("Lifestream"), null,
            L.T("Questionable requirement (indirect dependency).", "Questionableの前提プラグイン（間接依存）"));
        ImGui.EndTable();
    }

    private void DrawMountSelector(CharacterConfiguration settings)
    {
        DrawSectionTitle(L.T("Travel mount", "移動マウント"));
        IReadOnlyList<MountChoice> mounts;
        if (mountCache.ExpiresAt > DateTime.UtcNow)
            mounts = mountCache.Items;
        else
        {
            mounts = data.GetUnlockedMounts();
            mountCache = (DateTime.UtcNow.AddSeconds(10), mounts);
        }

        string selectedName = settings.MountId == 0
            ? L.T("Mount Roulette", "マウント・ルーレット")
            : mounts.FirstOrDefault(x => x.MountId == settings.MountId)?.Name ?? $"Mount {settings.MountId}";
        ImGui.SetNextItemWidth(360);
        if (!ImGui.BeginCombo(L.T("Mount used for travel", "移動時に使用するマウント"), selectedName))
            return;

        if (ImGui.Selectable(L.T("Mount Roulette", "マウント・ルーレット"), settings.MountId == 0))
        {
            settings.MountId = 0;
            save();
        }
        foreach (MountChoice mount in mounts)
        {
            if (!ImGui.Selectable(mount.Name, settings.MountId == mount.MountId))
                continue;
            settings.MountId = mount.MountId;
            save();
        }
        ImGui.EndCombo();
    }

    private static void DrawDependencyRow(string name, bool loaded, string? status, string role)
    {
        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        ImGui.TextUnformatted(name);
        ImGui.TableSetColumnIndex(1);
        ImGui.TextColored(loaded ? Green : Red, status ?? (loaded ? L.T("● Loaded", "● 読み込み済み") : L.T("● Not loaded", "● 未読み込み")));
        ImGui.TableSetColumnIndex(2);
        ImGui.TextWrapped(role);
    }

    private static void DrawSectionTitle(string title)
    {
        ImGui.Spacing();
        ImGui.TextDisabled(title);
        ImGui.Separator();
    }

    private bool IsPluginLoaded(string internalName)
    {
        try
        {
            return pluginInterface.InstalledPlugins.Any(x =>
                string.Equals(x.InternalName, internalName, StringComparison.OrdinalIgnoreCase) && x.IsLoaded);
        }
        catch
        {
            return false;
        }
    }

    private void DrawAetherCurrentMap()
    {
        IReadOnlyList<TerritoryProgress> all = data.GetAllTerritories();
        if (mapTerritoryId == 0)
            mapTerritoryId = data.GetTerritory(clientState.TerritoryType)?.TerritoryId
                             ?? data.GetExpansion(Settings.SelectedExpansion).FirstOrDefault()?.TerritoryId
                             ?? all.First().TerritoryId;

        TerritoryProgress? selected = data.GetTerritory(mapTerritoryId);
        string selectedName = selected?.Name ?? L.T("Select an area", "エリアを選択");
        ImGui.SetNextItemWidth(300);
        if (ImGui.BeginCombo(L.T("Displayed area", "表示エリア"), selectedName))
        {
            foreach (IGrouping<uint, TerritoryProgress> expansion in all.GroupBy(x => x.ExpansionId))
            {
                string expansionName = L.Expansion(expansion.Key);
                ImGui.Separator();
                ImGui.TextDisabled(expansionName);
                foreach (TerritoryProgress territory in expansion)
                {
                    if (ImGui.Selectable(territory.Name, territory.TerritoryId == mapTerritoryId))
                        mapTerritoryId = territory.TerritoryId;
                }
            }
            ImGui.EndCombo();
        }
        TerritoryProgress? currentTerritory = data.GetTerritory(clientState.TerritoryType);
        if (currentTerritory != null)
        {
            ImGui.SameLine();
            if (ImGui.Button(L.T("Current area", "現在エリア")))
                mapTerritoryId = currentTerritory.TerritoryId;
        }

        selected = data.GetTerritory(mapTerritoryId);
        if (selected == null)
        {
            ImGui.TextUnformatted(L.T("Select an area.", "エリアを選択してください。"));
            return;
        }

        TerritoryMapInfo? mapInfo = data.GetMapInfo(selected.TerritoryId);
        if (mapInfo == null)
        {
            ImGui.TextUnformatted(L.T("The map image for this area could not be loaded.", "このエリアのマップ画像を取得できません。"));
            return;
        }

        float mapSize = Math.Clamp(ImGui.GetContentRegionAvail().X, 320f, 640f);
        Vector2 origin = ImGui.GetCursorScreenPos();
        if (mapInfo.TexturePath != null)
        {
            var wrap = textureProvider.GetFromGame(mapInfo.TexturePath).GetWrapOrEmpty();
            ImGui.Image(wrap.Handle, new(mapSize, mapSize));
        }
        else
        {
            // Reserve the same map area so wind-current markers remain usable even if the
            // client cannot provide a map texture (modified/incomplete game data, etc.).
            ImGui.Dummy(new(mapSize, mapSize));
        }
        var draw = ImGui.GetWindowDrawList();

        if (mapInfo.TexturePath == null)
        {
            uint background = ImGui.ColorConvertFloat4ToU32(new Vector4(0.10f, 0.12f, 0.15f, 1f));
            uint grid = ImGui.ColorConvertFloat4ToU32(new Vector4(0.32f, 0.36f, 0.42f, 0.65f));
            draw.AddRectFilled(origin, origin + new Vector2(mapSize), background);
            for (int i = 0; i <= 8; i++)
            {
                float offset = mapSize * i / 8f;
                draw.AddLine(origin + new Vector2(offset, 0), origin + new Vector2(offset, mapSize), grid);
                draw.AddLine(origin + new Vector2(0, offset), origin + new Vector2(mapSize, offset), grid);
            }
            draw.AddText(origin + new Vector2(10, 10), 0xFFFFFFFF,
                L.T("Map unavailable; showing a coordinate grid", "マップ画像を取得できないため座標グリッドで表示中"));
        }

        uint unlockedColor = ImGui.ColorConvertFloat4ToU32(Green);
        uint lockedColor = ImGui.ColorConvertFloat4ToU32(Red);
        uint targetColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0.75f, 0.45f, 1f, 1f));
        uint aetheryteColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0.2f, 0.75f, 1f, 1f));
        uint outlineColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, 0.9f));
        Vector2 mouse = ImGui.GetMousePos();

        for (int i = 0; i < selected.FieldCurrents.Count; i++)
        {
            FieldCurrent field = selected.FieldCurrents[i];
            Vector2 mapCoordinate = MapUtil.WorldToMap(new Vector2(field.Position.X, field.Position.Z), mapInfo.Map);
            Vector2 normalized = new(
                Math.Clamp((mapCoordinate.X - 1f) / 41f, 0f, 1f),
                Math.Clamp((mapCoordinate.Y - 1f) / 41f, 0f, 1f));
            Vector2 point = origin + normalized * mapSize;
            bool unlocked = data.IsCurrentUnlocked(field.CurrentId);
            bool isTarget = automation.CurrentTarget?.CurrentId == field.CurrentId;
            uint color = isTarget ? targetColor : (unlocked ? unlockedColor : lockedColor);

            draw.AddCircleFilled(point, isTarget ? 9f : 7f, outlineColor);
            draw.AddCircleFilled(point, isTarget ? 7f : 5.5f, color);
            draw.AddText(point + new Vector2(7f, -9f), outlineColor, (i + 1).ToString());
            draw.AddText(point + new Vector2(6f, -10f), 0xFFFFFFFF, (i + 1).ToString());

            if (Vector2.Distance(mouse, point) <= 12f)
            {
                ImGui.BeginTooltip();
                ImGui.TextUnformatted(L.T($"#{i + 1} Aether Current {field.CurrentId}", $"#{i + 1} 風脈 {field.CurrentId}"));
                ImGui.TextUnformatted(unlocked ? L.T("Unlocked", "解放済み") : isTarget ? L.T("Current travel target", "現在の移動対象") : L.T("Locked", "未解放"));
                ImGui.TextUnformatted(L.T($"Map coordinates X:{mapCoordinate.X:F1} Y:{mapCoordinate.Y:F1}", $"マップ座標 X:{mapCoordinate.X:F1} Y:{mapCoordinate.Y:F1}"));
                ImGui.EndTooltip();
            }
        }

        IReadOnlyList<TeleportDestination> unlockedAetherytes;
        if (aetheryteMapCache.TryGetValue(selected.TerritoryId, out var cachedAetherytes) &&
            cachedAetherytes.ExpiresAt > DateTime.UtcNow)
            unlockedAetherytes = cachedAetherytes.Items;
        else
        {
            unlockedAetherytes = data.GetUnlockedAetherytes(selected.TerritoryId);
            aetheryteMapCache[selected.TerritoryId] = (DateTime.UtcNow.AddSeconds(2), unlockedAetherytes);
        }
        for (int i = 0; i < unlockedAetherytes.Count; i++)
        {
            TeleportDestination destination = unlockedAetherytes[i];
            Vector2 mapCoordinate = MapUtil.WorldToMap(new Vector2(destination.Position.X, destination.Position.Z), mapInfo.Map);
            Vector2 normalized = new(
                Math.Clamp((mapCoordinate.X - 1f) / 41f, 0f, 1f),
                Math.Clamp((mapCoordinate.Y - 1f) / 41f, 0f, 1f));
            Vector2 point = origin + normalized * mapSize;

            bool selectedForTravel = automation.CurrentAetheryteId == destination.AetheryteId;
            draw.AddCircleFilled(point, selectedForTravel ? 12f : 9f, outlineColor);
            draw.AddCircleFilled(point, selectedForTravel ? 9f : 7f,
                selectedForTravel ? targetColor : aetheryteColor);
            string label = $"A{i + 1}";
            draw.AddText(point + new Vector2(8f, -9f), outlineColor, label);
            draw.AddText(point + new Vector2(7f, -10f), 0xFFFFFFFF, label);

            if (Vector2.Distance(mouse, point) <= 13f)
            {
                ImGui.BeginTooltip();
                ImGui.TextUnformatted(L.T($"Aetheryte candidate: {destination.Name}", $"エーテライト候補: {destination.Name}"));
                if (selectedForTravel)
                    ImGui.TextColored(new Vector4(0.75f, 0.45f, 1f, 1f), L.T("Current teleport candidate", "現在のテレポ候補"));
                ImGui.TextUnformatted($"ID: {destination.AetheryteId}  SubIndex: {destination.SubIndex}");
                ImGui.TextUnformatted(L.T($"World coordinates X:{destination.Position.X:F1} Y:{destination.Position.Y:F1} Z:{destination.Position.Z:F1}", $"ワールド座標 X:{destination.Position.X:F1} Y:{destination.Position.Y:F1} Z:{destination.Position.Z:F1}"));
                ImGui.TextUnformatted(L.T($"Map coordinates X:{mapCoordinate.X:F1} Y:{mapCoordinate.Y:F1}", $"マップ座標 X:{mapCoordinate.X:F1} Y:{mapCoordinate.Y:F1}"));
                ImGui.EndTooltip();
            }
        }

        ImGui.TextColored(Green, L.T("● Unlocked", "● 解放済み"));
        ImGui.SameLine();
        ImGui.TextColored(Red, L.T("● Locked", "● 未解放"));
        ImGui.SameLine();
        ImGui.TextColored(new Vector4(0.75f, 0.45f, 1f, 1f), L.T("● Current travel target", "● 現在の移動対象"));
        ImGui.SameLine();
        ImGui.TextColored(new Vector4(0.2f, 0.75f, 1f, 1f), L.T("● Aetheryte candidate", "● エーテライト候補"));
        if (unlockedAetherytes.Count == 0)
            ImGui.TextColored(Yellow, L.T("Could not obtain normal aetheryte coordinates for this area.", "このエリアの通常エーテライト座標を取得できませんでした。"));
        if (Settings.ShowDebugInformation)
            ImGui.TextDisabled(L.T($"Aetheryte diagnostics: {data.LastAetheryteScanStatus}", $"エーテライト取得診断: {data.LastAetheryteScanStatus}"));
        if (mapInfo.TexturePath == null)
            ImGui.TextColored(Yellow, L.T("The game map image was not found; using the fallback display.", "ゲーム内マップ画像が見つからないため、フォールバック表示を使用しています。"));
        ImGui.TextWrapped(L.T("Hover a number to view its Aether Current ID, unlock state, and in-game map coordinates.", "番号にマウスを合わせると風脈ID・解放状態・ゲーム内マップ座標を表示します。"));
    }

    private void DrawExpansion(uint expansionId, string name)
    {
        IReadOnlyList<TerritoryProgress> territories = data.GetExpansion(expansionId);
        int fieldTotal = territories.Sum(x => x.FieldCurrents.Count);
        int fieldDone = territories.Sum(x => x.FieldCurrents.Count(y => data.IsCurrentUnlocked(y.CurrentId)));
        int questTotal = territories.Sum(x => x.QuestIds.Count);
        int questDone = territories.Sum(x => x.QuestIds.Count(data.IsQuestComplete));
        bool complete = fieldDone == fieldTotal && questDone == questTotal;
        bool open = DrawProgressHeader(name, $"exp{expansionId}", fieldDone, fieldTotal, questDone, questTotal,
            complete ? ImGuiTreeNodeFlags.None : ImGuiTreeNodeFlags.DefaultOpen,
            expansionId == Settings.SelectedExpansion);
        if (open)
        {
            foreach (TerritoryProgress territory in territories)
                DrawTerritory(territory, false);
            ImGui.TreePop();
        }
    }

    private void DrawTerritory(TerritoryProgress territory, bool defaultOpen)
    {
        int fieldDone = territory.FieldCurrents.Count(x => data.IsCurrentUnlocked(x.CurrentId));
        int questDone = territory.QuestIds.Count(data.IsQuestComplete);
        ImGuiTreeNodeFlags flags = defaultOpen ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None;
        bool open = DrawProgressHeader(territory.Name, $"ter{territory.TerritoryId}", fieldDone,
            territory.FieldCurrents.Count, questDone, territory.QuestIds.Count, flags);
        if (open)
        {
            if (ImGui.BeginTable($"ProgressItems##{territory.TerritoryId}", 3,
                    ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp))
            {
                ImGui.TableSetupColumn(L.T("Type", "種別"), ImGuiTableColumnFlags.WidthFixed, 110);
                ImGui.TableSetupColumn(L.T("Target", "対象"));
                ImGui.TableSetupColumn(L.T("Action", "操作"), ImGuiTableColumnFlags.WidthFixed, 38);
                ImGui.TableHeadersRow();

                foreach (FieldCurrent field in territory.FieldCurrents)
                {
                    bool done = data.IsCurrentUnlocked(field.CurrentId);
                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(0);
                    ImGui.TextColored(done ? Green : Yellow, $"{(done ? "✓" : "○")} {L.T("Field", "フィールド")}");
                    ImGui.TableSetColumnIndex(1);
                    ImGui.TextUnformatted(L.T("Field Aether Current", "フィールド風脈"));
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip($"ID: {field.CurrentId}\nX:{field.Position.X:F1}  Y:{field.Position.Y:F1}  Z:{field.Position.Z:F1}");
                    ImGui.TableSetColumnIndex(2);
                    ImGui.BeginDisabled(done || automation.IsRunning);
                    if (DrawSmallIconButton($"field-{territory.TerritoryId}-{field.CurrentId}", FontAwesomeIcon.Play))
                        automation.StartSingleField(field);
                    ImGui.EndDisabled();
                    if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                        ImGui.SetTooltip(L.T("Unlock this Aether Current", "この風脈を解放"));
                }

                foreach (ushort id in territory.QuestIds)
                {
                    bool done = data.IsQuestComplete(id);
                    (string detail, Vector4 color) = GetQuestStatus(id, done);
                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(0);
                    ImGui.TextColored(color, $"{(done ? "✓" : "○")} {L.T("Quest", "クエスト")}");
                    ImGui.TableSetColumnIndex(1);
                    ImGui.BeginGroup();
                    ImGui.TextUnformatted(data.GetQuestName(id));
                    ImGui.SameLine();
                    ImGui.TextDisabled($"— {detail}");
                    ImGui.EndGroup();
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip($"ID: {id}\n{detail}");
                    ImGui.TableSetColumnIndex(2);
                    ImGui.BeginDisabled(done || automation.IsRunning);
                    if (DrawSmallIconButton($"quest-{territory.TerritoryId}-{id}", FontAwesomeIcon.Play))
                        automation.StartSingleQuest(id, territory.TerritoryId);
                    ImGui.EndDisabled();
                    if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                        ImGui.SetTooltip(L.T("Run this quest", "このクエストを進行"));
                }
                ImGui.EndTable();
            }
            ImGui.TreePop();
        }
    }

    private static bool DrawProgressHeader(string label, string id, int fieldDone, int fieldTotal,
        int questDone, int questTotal, ImGuiTreeNodeFlags flags, bool selected = false)
    {
        bool open = false;
        if (!ImGui.BeginTable($"ProgressHeader##{id}", 3, ImGuiTableFlags.SizingStretchProp))
            return false;

        ImGui.TableSetupColumn("Title", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Field", ImGuiTableColumnFlags.WidthFixed, 135);
        ImGui.TableSetupColumn("Quest", ImGuiTableColumnFlags.WidthFixed, 135);
        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        if (selected)
            ImGui.SetNextItemOpen(true, ImGuiCond.Once);
        open = ImGui.TreeNodeEx($"{label}##{id}", flags);
        ImGui.TableSetColumnIndex(1);
        ImGui.TextDisabled($"{L.T("Field", "フィールド"),-8} {fieldDone,2}/{fieldTotal,-2}");
        ImGui.TableSetColumnIndex(2);
        ImGui.TextDisabled($"{L.T("Quest", "クエスト"),-8} {questDone,2}/{questTotal,-2}");
        ImGui.EndTable();
        return open;
    }

    private void DrawConfirmationPopup()
    {
        if (!confirmExpansion)
            return;
        if (!ImGui.BeginPopupModal(L.T("Confirm expansion run", "拡張単位の実行確認"), ref confirmExpansion, ImGuiWindowFlags.AlwaysAutoResize))
            return;

        string expansion = L.Expansion(Settings.SelectedExpansion);
        ImGui.TextWrapped(L.T(
            $"Process locked Aether Currents in “{expansion}” in sequence. Normal teleport fees apply, and Questionable runs each Aether Current quest individually.",
            $"「{expansion}」の未解放風脈を順番に処理します。通常テレポ代が発生し、風脈クエストではQuestionableが各クエストを単独実行します。"));
        if (ImGui.Button(L.T("Start", "開始する")))
        {
            StartExpansion();
            confirmExpansion = false;
            ImGui.CloseCurrentPopup();
        }
        ImGui.SameLine();
        if (ImGui.Button(L.T("Cancel", "キャンセル")))
        {
            confirmExpansion = false;
            ImGui.CloseCurrentPopup();
        }
        ImGui.EndPopup();
    }

    private void StartExpansion() => automation.StartExpansion(Settings.SelectedExpansion, Settings.IncludeFieldCurrents, Settings.IncludeQuestCurrents);

    private (string Text, Vector4 Color) GetQuestStatus(ushort questId, bool complete)
    {
        if (complete)
            return (L.T("Unlocked", "解放済み"), Green);
        if (!questionable.Available)
            return (L.T("Locked (Questionable not detected)", "未解放（Questionable未検出）"), Red);
        if (questStatusCache.TryGetValue(questId, out var cached) && cached.ExpiresAt > DateTime.UtcNow)
            return (cached.Text, cached.Color);

        string text;
        Vector4 color;
        if (questionable.IsLocked(questId))
        {
            text = questionable.GetLockedReason(questId);
            color = Red;
        }
        else
        {
            text = L.T("Available", "実行可能");
            color = Yellow;
        }
        questStatusCache[questId] = (DateTime.UtcNow.AddSeconds(5), text, color);
        return (text, color);
    }

    private static bool DrawIconButton(string id, FontAwesomeIcon icon, Vector2 size)
    {
        ImGui.PushFont(UiBuilder.IconFont);
        bool clicked = ImGui.Button($"{icon.ToIconString()}##{id}", size);
        ImGui.PopFont();
        return clicked;
    }

    private static bool DrawSmallIconButton(string id, FontAwesomeIcon icon)
    {
        ImGui.PushFont(UiBuilder.IconFont);
        bool clicked = ImGui.SmallButton($"{icon.ToIconString()}##{id}");
        ImGui.PopFont();
        return clicked;
    }
}
