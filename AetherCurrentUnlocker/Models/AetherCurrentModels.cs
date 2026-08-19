using System.Numerics;
using Lumina.Excel.Sheets;

namespace AetherCurrentUnlocker.Models;

internal sealed record FieldCurrent(uint TerritoryId, uint CurrentId, uint DataId, Vector3 Position);

internal sealed record MountChoice(uint MountId, string Name);

internal sealed record TeleportDestination(
    uint AetheryteId,
    byte SubIndex,
    string Name,
    Vector3 Position);

internal sealed record TerritoryProgress(
    uint TerritoryId,
    string EnglishName,
    string JapaneseName,
    uint ExpansionId,
    IReadOnlyList<FieldCurrent> FieldCurrents,
    IReadOnlyList<ushort> QuestIds)
{
    public string Name => L.IsJapanese ? JapaneseName : EnglishName;
}

internal sealed record TerritoryMapInfo(Map Map, string? TexturePath);

internal enum AutomationMode
{
    None,
    CurrentTerritory,
    Expansion,
    SingleTarget
}

internal enum AutomationPhase
{
    Idle,
    Selecting,
    Teleporting,
    Moving,
    Interacting,
    RunningQuest,
    Waiting,
    Completed,
    Stopped,
    Error
}
