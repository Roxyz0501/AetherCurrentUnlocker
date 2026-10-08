using Dalamud.Configuration;
using Dalamud.Game;
using Newtonsoft.Json;

namespace AetherCurrentUnlocker;

public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 4;
    public string? Language { get; set; }

    [JsonProperty("DisplayLanguage")]
    public DisplayLanguage? LegacyDisplayLanguage { get; set; }
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
        Version = 4;
        return character;
    }

    public bool Migrate(ClientLanguage? gameLanguage, string? dalamudUiLanguage)
    {
        bool changed = false;
        string? normalized = L.NormalizeLanguageCode(Language);
        if (normalized == null)
            normalized = LegacyDisplayLanguage switch
            {
                DisplayLanguage.English => "en",
                DisplayLanguage.Japanese => "ja",
                _ => L.ResolveInitial(gameLanguage, dalamudUiLanguage),
            };
        if (!string.Equals(Language, normalized, StringComparison.Ordinal))
        {
            Language = normalized;
            changed = true;
        }
        if (LegacyDisplayLanguage != null)
        {
            LegacyDisplayLanguage = null;
            changed = true;
        }
        if (Version < 4)
        {
            Version = 4;
            changed = true;
        }
        return changed;
    }

    public bool ShouldSerializeLegacyDisplayLanguage() => false;
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
