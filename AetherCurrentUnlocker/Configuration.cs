using Dalamud.Configuration;

namespace AetherCurrentUnlocker;

public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 3;
    public DisplayLanguage? DisplayLanguage { get; set; }
    public Dictionary<ulong, CharacterConfiguration> Characters { get; set; } = [];

    // Version 1 migration defaults. These remain readable for existing installations.
    public uint SelectedExpansion { get; set; } = 5;
    public bool IncludeFieldCurrents { get; set; } = true;
    public bool IncludeQuestCurrents { get; set; } = true;
    public bool ConfirmExpansionRun { get; set; } = true;
    public bool DebugMode { get; set; }

    public CharacterConfiguration ForCharacter(ulong contentId)
    {
        if (Characters.TryGetValue(contentId, out CharacterConfiguration? character))
            return character;

        character = new CharacterConfiguration
        {
            SelectedExpansion = SelectedExpansion,
            IncludeFieldCurrents = IncludeFieldCurrents,
            IncludeQuestCurrents = IncludeQuestCurrents,
            ConfirmExpansionRun = ConfirmExpansionRun,
            ShowDebugInformation = DebugMode,
        };
        Characters[contentId] = character;
        Version = 3;
        return character;
    }

    public bool Migrate(Dalamud.Game.ClientLanguage? clientLanguage)
    {
        bool changed = false;
        DisplayLanguage resolved = L.ResolveInitial(DisplayLanguage, clientLanguage);
        if (DisplayLanguage != resolved)
        {
            DisplayLanguage = resolved;
            changed = true;
        }
        if (Version < 3)
        {
            Version = 3;
            changed = true;
        }
        return changed;
    }
}

public sealed class CharacterConfiguration
{
    public uint SelectedExpansion { get; set; } = 5;
    public bool IncludeFieldCurrents { get; set; } = true;
    public bool IncludeQuestCurrents { get; set; } = true;
    public bool ConfirmExpansionRun { get; set; } = true;
    public bool ShowDebugInformation { get; set; }

    // 0 means the game's Mount Roulette general action.
    public uint MountId { get; set; }
}
