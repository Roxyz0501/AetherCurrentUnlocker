using AetherCurrentUnlocker.Models;
using Dalamud.Game;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;

namespace AetherCurrentUnlocker.Data;

internal sealed class AetherCurrentDataService
{
    private static readonly IReadOnlyDictionary<uint, ushort[]> QuestIdsByTerritory =
        new Dictionary<uint, ushort[]>
        {
            // Heavensward
            [397] = [1744, 1759, 1760, 2111], [398] = [1771, 1790, 1797, 1802],
            [399] = [1936, 1945, 1963, 1966], [400] = [1819, 1823, 1828, 1835],
            [401] = [1748, 1874, 1909, 1910],
            // Stormblood
            [612] = [2639, 2661, 2816, 2821], [613] = [2632, 2673, 2687, 2693],
            [614] = [2724, 2728, 2730, 2733], [620] = [2655, 2842, 2851, 2860],
            [621] = [2877, 2880, 2881, 2883], [622] = [2760, 2771, 2782, 2791],
            // Shadowbringers
            [813] = [3380, 3384, 3385, 3386], [814] = [3360, 3371, 3537, 3556],
            [815] = [3375, 3503, 3511, 3525], [816] = [3395, 3398, 3404, 3427],
            [817] = [3444, 3467, 3478, 3656], [818] = [3588, 3592, 3593, 3594],
            // Endwalker
            [956] = [4320, 4329, 4480, 4484], [957] = [4203, 4257, 4259, 4489],
            [958] = [4216, 4232, 4498, 4502], [959] = [4240, 4241, 4253, 4516],
            [960] = [4342, 4346, 4354, 4355], [961] = [4288, 4313, 4507, 4511],
            // Dawntrail
            [1187] = [5039, 5047, 5051, 5055], [1188] = [5064, 5074, 5081, 5085],
            [1189] = [5094, 5103, 5110, 5114], [1190] = [5130, 5138, 5140, 5144],
            [1191] = [5153, 5156, 5159, 5160], [1192] = [5174, 5176, 5178, 5179]
        };

    public static readonly IReadOnlyList<uint> ExpansionIds = [1, 2, 3, 4, 5];

    private readonly IDataManager data;
    private readonly IAetheryteList aetheryteList;
    private readonly IReadOnlyDictionary<uint, TerritoryProgress> territories;
    private readonly IReadOnlyDictionary<ushort, string> questNamesEnglish;
    private readonly IReadOnlyDictionary<ushort, string> questNamesJapanese;
    private readonly IReadOnlyDictionary<ushort, (uint TerritoryId, System.Numerics.Vector3 Position)> questStartPositions;
    private readonly IReadOnlySet<uint> mountTerritories;
    public string LastAetheryteScanStatus { get; private set; } = L.T("Not scanned", "未走査");

    public AetherCurrentDataService(IDataManager data, IAetheryteList aetheryteList)
    {
        this.data = data;
        this.aetheryteList = aetheryteList;
        mountTerritories = data.GetExcelSheet<TerritoryType>()
            .Where(x => x.RowId > 0 && x.Mount)
            .Select(x => x.RowId)
            .ToHashSet();
        (questNamesEnglish, questStartPositions) = BuildQuestMetadata(ClientLanguage.English);
        (questNamesJapanese, _) = BuildQuestMetadata(ClientLanguage.Japanese);
        territories = BuildTerritories();
    }

    public IReadOnlyList<TerritoryProgress> GetAllTerritories() => territories.Values.OrderBy(x => x.ExpansionId).ThenBy(x => x.TerritoryId).ToList();
    public IReadOnlyList<TerritoryProgress> GetExpansion(uint expansionId) => territories.Values.Where(x => x.ExpansionId == expansionId).OrderBy(x => x.TerritoryId).ToList();
    public TerritoryProgress? GetTerritory(uint territoryId) => territories.GetValueOrDefault(territoryId);
    public string GetQuestName(ushort questId)
    {
        IReadOnlyDictionary<ushort, string> names = L.IsJapanese ? questNamesJapanese : questNamesEnglish;
        return names.GetValueOrDefault(questId, L.T($"Quest {questId}", $"クエスト {questId}"));
    }
    public System.Numerics.Vector3? GetQuestStartPosition(ushort questId, uint territoryId) =>
        questStartPositions.TryGetValue(questId, out var start) && start.TerritoryId == territoryId
            ? start.Position
            : null;
    public bool CanUseMount(uint territoryId) => mountTerritories.Contains(territoryId);

    public unsafe IReadOnlyList<MountChoice> GetUnlockedMounts()
    {
        PlayerState* state = PlayerState.Instance();
        if (state == null)
            return [];

        return data.GetExcelSheet<Mount>(L.GameDataLanguage)
            .Where(x => x.RowId is > 0 and <= ushort.MaxValue &&
                        x.Order != -1 && state->IsMountUnlocked((ushort)x.RowId))
            .Select(x => new MountChoice(x.RowId,
                string.IsNullOrWhiteSpace(x.Singular.ToString()) ? $"Mount {x.RowId}" : x.Singular.ToString()))
            .OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public TerritoryMapInfo? GetMapInfo(uint territoryId)
    {
        TerritoryType territory = data.GetExcelSheet<TerritoryType>().GetRow(territoryId);
        if (territory.Map.RowId == 0)
            return null;

        Lumina.Excel.Sheets.Map map = data.GetExcelSheet<Lumina.Excel.Sheets.Map>().GetRow(territory.Map.RowId);
        string mapId = map.Id.ToString();
        if (string.IsNullOrWhiteSpace(mapId))
            return null;
        // Game map filenames remove the slash entirely:
        // Map.Id "r2f1/00" -> "ui/map/r2f1/00/r2f100_m.tex".
        string fileId = mapId.Replace("/", string.Empty);
        string mediumPath = $"ui/map/{mapId}/{fileId}_m.tex";
        string smallPath = $"ui/map/{mapId}/{fileId}_s.tex";
        string? texturePath = data.FileExists(mediumPath)
            ? mediumPath
            : data.FileExists(smallPath) ? smallPath : null;
        return new(map, texturePath);
    }

    public unsafe bool IsCurrentUnlocked(uint currentId)
    {
        PlayerState* state = PlayerState.Instance();
        return state != null && state->IsAetherCurrentUnlocked(currentId);
    }

    public bool IsQuestComplete(ushort questId) => QuestManager.IsQuestComplete(questId);

    public IReadOnlyList<TeleportDestination> FindUnlockedAetherytesByDistance(
        uint territoryId, System.Numerics.Vector3 targetPosition)
    {
        var listEntries = aetheryteList.Where(x => x.TerritoryId == territoryId).ToList();
        var sheet = data.GetExcelSheet<Aetheryte>(L.GameDataLanguage);
        int normalInTerritory = 0;
        int coordinateCount = 0;
        List<(TeleportDestination Destination, float DistanceSquared)> destinations = [];

        // 実行時のLevel解決が0件でも動作するよう、同梱座標を必ず基準にする。
        // Dalamud一覧とExcelはSubIndexおよびローカライズ済み名称の補完にだけ使う。
        foreach (TeleportDestination baked in AetheryteCatalog.Get(territoryId))
        {
            Aetheryte? excel = sheet.GetRowOrDefault(baked.AetheryteId);
            var matchingEntry = listEntries.FirstOrDefault(x => x.AetheryteId == baked.AetheryteId);
            byte subIndex = matchingEntry?.SubIndex ?? baked.SubIndex;
            string? name = excel?.PlaceName.ValueNullable?.Name.ToString();
            if (string.IsNullOrWhiteSpace(name))
                name = baked.Name;
            AddDestination(new(baked.AetheryteId, subIndex, name, baked.Position));
        }

        foreach (Aetheryte aetheryte in sheet)
        {
            if (!aetheryte.IsAetheryte || aetheryte.Territory.RowId != territoryId)
                continue;
            normalInTerritory++;

            // 一覧側のTerritoryIdを解放済み判定の正とする。Level.Territoryが空または
            // 別IDになるデータでは、最初の有効なLevel座標へフォールバックする。
            Level? level = aetheryte.Level
                .Select(x => x.ValueNullable)
                .FirstOrDefault(x => x.HasValue && x.Value.Territory.RowId == territoryId)
                ?? aetheryte.Level.Select(x => x.ValueNullable).FirstOrDefault(x => x.HasValue);
            if (level == null)
                continue;
            coordinateCount++;

            // 同梱済みIDは検証用に数えるだけで、座標は安定した同梱値を優先する。
            if (AetheryteCatalog.Get(territoryId).Any(x => x.AetheryteId == aetheryte.RowId))
                continue;

            System.Numerics.Vector3 position = new(level.Value.X, level.Value.Y, level.Value.Z);
            var matchingEntry = listEntries.FirstOrDefault(x => x.AetheryteId == aetheryte.RowId);
            byte subIndex = matchingEntry?.SubIndex ?? 0;
            string name = aetheryte.PlaceName.ValueNullable?.Name.ToString()
                          ?? L.T($"Aetheryte {aetheryte.RowId}", $"エーテライト {aetheryte.RowId}");
            AddDestination(new(aetheryte.RowId, subIndex, name, position));
        }

        int bakedCount = AetheryteCatalog.Get(territoryId).Count;
        LastAetheryteScanStatus = L.T(
            $"Territory {territoryId}: Dalamud list {aetheryteList.Length} / territory matches {listEntries.Count} / " +
            $"bundled coordinates {bakedCount} / normal Excel entries {normalInTerritory} / Excel coordinates {coordinateCount}",
            $"Territory {territoryId}: Dalamud一覧 {aetheryteList.Length}件 / エリア一致 {listEntries.Count}件 / " +
            $"同梱座標 {bakedCount}件 / Excel通常 {normalInTerritory}件 / Excel座標 {coordinateCount}件");

        return destinations
            .GroupBy(x => (x.Destination.AetheryteId, x.Destination.SubIndex))
            .Select(x => x.OrderBy(y => y.DistanceSquared).First())
            .OrderBy(x => x.DistanceSquared)
            .Select(x => x.Destination)
            .ToList();

        void AddDestination(TeleportDestination destination)
        {
            float dx = destination.Position.X - targetPosition.X;
            float dz = destination.Position.Z - targetPosition.Z;
            // 「最寄り」はゲーム内地図上の水平距離で決める。
            destinations.Add((destination, dx * dx + dz * dz));
        }
    }

    public IReadOnlyList<TeleportDestination> GetUnlockedAetherytes(uint territoryId) =>
        FindUnlockedAetherytesByDistance(territoryId, System.Numerics.Vector3.Zero);

    private IReadOnlyDictionary<uint, TerritoryProgress> BuildTerritories()
    {
        Dictionary<uint, TerritoryProgress> result = [];
        var territorySheetEnglish = data.GetExcelSheet<TerritoryType>(ClientLanguage.English);
        var territorySheetJapanese = data.GetExcelSheet<TerritoryType>(ClientLanguage.Japanese);
        var flagSheet = data.GetExcelSheet<AetherCurrentCompFlgSet>();

        foreach ((uint territoryId, ushort[] questIds) in QuestIdsByTerritory)
        {
            TerritoryType territory = territorySheetEnglish.GetRow(territoryId);
            TerritoryType territoryJapanese = territorySheetJapanese.GetRow(territoryId);
            HashSet<uint> validFieldIds = [];
            if (territory.AetherCurrentCompFlgSet.RowId > 0)
            {
                AetherCurrentCompFlgSet flags = flagSheet.GetRow(territory.AetherCurrentCompFlgSet.RowId);
                validFieldIds.UnionWith(flags.AetherCurrents
                    .Where(x => x.RowId > 0 && x.Value.Quest.RowId == 0)
                    .Select(x => x.RowId));
            }

            List<FieldCurrent> fieldCurrents = FieldCurrentCatalog.All
                .Where(x => x.TerritoryId == territoryId && validFieldIds.Contains(x.CurrentId))
                .OrderBy(x => x.CurrentId)
                .ToList();

            string englishName = territory.PlaceName.ValueNullable?.Name.ToString()
                                 ?? territory.PlaceNameZone.ValueNullable?.Name.ToString()
                                 ?? territoryId.ToString();
            string japaneseName = territoryJapanese.PlaceName.ValueNullable?.Name.ToString()
                                  ?? territoryJapanese.PlaceNameZone.ValueNullable?.Name.ToString()
                                  ?? englishName;
            result[territoryId] = new(territoryId, englishName, japaneseName, territory.ExVersion.RowId,
                fieldCurrents, questIds);
        }

        return result;
    }

    private (IReadOnlyDictionary<ushort, string> Names,
        IReadOnlyDictionary<ushort, (uint TerritoryId, System.Numerics.Vector3 Position)> StartPositions) BuildQuestMetadata(
            ClientLanguage language)
    {
        HashSet<ushort> wanted = QuestIdsByTerritory.Values.SelectMany(x => x).ToHashSet();
        List<Quest> quests = data.GetExcelSheet<Quest>(language)
            .Where(x => wanted.Contains((ushort)(x.RowId & 0xFFFF)) && !x.Name.IsEmpty)
            .GroupBy(x => (ushort)(x.RowId & 0xFFFF))
            .Select(x => x.First())
            .ToList();
        var names = quests.ToDictionary(x => (ushort)(x.RowId & 0xFFFF), x => x.Name.ToString());
        var positions = quests
            .Where(x => x.IssuerLocation.ValueNullable != null)
            .ToDictionary(
                x => (ushort)(x.RowId & 0xFFFF),
                x =>
                {
                    Level level = x.IssuerLocation.Value;
                    return (level.Territory.RowId, new System.Numerics.Vector3(level.X, level.Y, level.Z));
                });
        return (names, positions);
    }
}
