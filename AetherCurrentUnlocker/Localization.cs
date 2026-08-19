using Dalamud.Game;

namespace AetherCurrentUnlocker;

public enum DisplayLanguage
{
    English = 1,
    Japanese = 2,
}

internal static class L
{
    private static Func<DisplayLanguage?> configuredLanguage = () => DisplayLanguage.English;

    public static bool IsJapanese => configuredLanguage() == DisplayLanguage.Japanese;
    public static ClientLanguage GameDataLanguage => IsJapanese ? ClientLanguage.Japanese : ClientLanguage.English;

    public static void Configure(Func<DisplayLanguage?> configured)
    {
        configuredLanguage = configured;
    }

    public static string T(string english, string japanese) => IsJapanese ? japanese : english;

    public static string Expansion(uint id) => id switch
    {
        1 => T("Heavensward", "蒼天のイシュガルド"),
        2 => T("Stormblood", "紅蓮のリベレーター"),
        3 => T("Shadowbringers", "漆黒のヴィランズ"),
        4 => T("Endwalker", "暁月のフィナーレ"),
        5 => T("Dawntrail", "黄金のレガシー"),
        _ => id.ToString(),
    };

    internal static DisplayLanguage ResolveInitial(DisplayLanguage? saved, ClientLanguage? client) => saved switch
    {
        DisplayLanguage.English => DisplayLanguage.English,
        DisplayLanguage.Japanese => DisplayLanguage.Japanese,
        _ => client == ClientLanguage.Japanese ? DisplayLanguage.Japanese : DisplayLanguage.English,
    };

    internal static void VerifyResolution()
    {
        if (ResolveInitial(null, ClientLanguage.Japanese) != DisplayLanguage.Japanese ||
            ResolveInitial(null, ClientLanguage.English) != DisplayLanguage.English ||
            ResolveInitial(null, null) != DisplayLanguage.English ||
            ResolveInitial(DisplayLanguage.English, ClientLanguage.Japanese) != DisplayLanguage.English ||
            ResolveInitial(DisplayLanguage.Japanese, ClientLanguage.English) != DisplayLanguage.Japanese ||
            ResolveInitial((DisplayLanguage)0, ClientLanguage.Japanese) != DisplayLanguage.Japanese)
            throw new InvalidOperationException("Language resolution self-check failed.");
    }
}
