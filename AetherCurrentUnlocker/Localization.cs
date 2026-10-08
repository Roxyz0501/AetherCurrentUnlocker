using System.Globalization;
using Dalamud.Game;

namespace AetherCurrentUnlocker;

// Values 1 and 2 are retained for migration from the published 0.3.1.0 configuration.
public enum DisplayLanguage
{
    English = 1,
    Japanese = 2,
    German = 3,
    French = 4,
    Korean = 5,
    SimplifiedChinese = 6,
    TraditionalChinese = 7,
}

internal static class L
{
    public static readonly IReadOnlyList<string> SupportedCodes =
        ["ja", "en", "de", "fr", "ko", "zh-Hans", "zh-Hant"];

    private static Func<string?> configuredLanguage = () => "en";

    public static string Code => NormalizeLanguageCode(configuredLanguage()) ?? "en";
    public static bool IsJapanese => Code == "ja";
    public static ClientLanguage GameDataLanguage => Code switch
    {
        "ja" => ClientLanguage.Japanese,
        "de" => ClientLanguage.German,
        "fr" => ClientLanguage.French,
        _ => ClientLanguage.English,
    };

    public static void Configure(Func<string?> configured) => configuredLanguage = configured;

    public static string T(string english, string japanese)
    {
        if (Code == "en")
            return english;
        if (Code == "ja")
            return japanese;
        return LocalizationCatalog.Get(english, Code) ?? english;
    }

    public static string F(string key, params object?[] args) =>
        string.Format(CultureInfo.InvariantCulture, LocalizationCatalog.GetRequired(key, Code), args);

    public static string LanguageName(string code) => code switch
    {
        "ja" => "日本語",
        "en" => "English",
        "de" => "Deutsch",
        "fr" => "Français",
        "ko" => "한국어",
        "zh-Hans" => "简体中文",
        "zh-Hant" => "繁體中文",
        _ => code,
    };

    public static string Expansion(uint id) => F(id switch
    {
        1 => "expansion.heavensward",
        2 => "expansion.stormblood",
        3 => "expansion.shadowbringers",
        4 => "expansion.endwalker",
        5 => "expansion.dawntrail",
        _ => "expansion.unknown",
    }, id);

    internal static string ResolveInitial(ClientLanguage? gameLanguage, string? dalamudUiLanguage)
    {
        string? game = gameLanguage switch
        {
            ClientLanguage.Japanese => "ja",
            ClientLanguage.English => "en",
            ClientLanguage.German => "de",
            ClientLanguage.French => "fr",
            _ => null,
        };
        return game ?? NormalizeLanguageCode(dalamudUiLanguage) ?? "en";
    }

    internal static string? NormalizeLanguageCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        string normalized = value.Trim().Replace('_', '-');
        string lower = normalized.ToLowerInvariant();
        if (lower is "ja" or "ja-jp") return "ja";
        if (lower is "en" or "en-us" or "en-gb") return "en";
        if (lower is "de" or "de-de") return "de";
        if (lower is "fr" or "fr-fr") return "fr";
        if (lower is "ko" or "ko-kr") return "ko";
        if (lower is "zh-hans" or "zh-cn" or "zh-sg") return "zh-Hans";
        if (lower is "zh-hant" or "zh-tw" or "zh-hk" or "zh-mo") return "zh-Hant";
        return null;
    }

    internal static void VerifyResolution()
    {
        if (ResolveInitial(ClientLanguage.Japanese, "en") != "ja" ||
            ResolveInitial(ClientLanguage.English, "ja") != "en" ||
            ResolveInitial(ClientLanguage.German, null) != "de" ||
            ResolveInitial(ClientLanguage.French, null) != "fr" ||
            ResolveInitial(null, "ko-KR") != "ko" ||
            ResolveInitial(null, "zh-CN") != "zh-Hans" ||
            ResolveInitial(null, "zh-TW") != "zh-Hant" ||
            ResolveInitial(null, "zh") != "en" ||
            ResolveInitial(null, "unknown") != "en" ||
            NormalizeLanguageCode("EN_us") != "en" ||
            NormalizeLanguageCode("zh-Hant") != "zh-Hant")
            throw new InvalidOperationException("Language resolution self-check failed.");
        LocalizationCatalog.Verify();
    }
}
