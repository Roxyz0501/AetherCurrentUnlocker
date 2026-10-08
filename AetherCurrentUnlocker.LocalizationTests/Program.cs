using AetherCurrentUnlocker;
using Dalamud.Game;

L.VerifyResolution();

AssertMigration(new Configuration(), ClientLanguage.Japanese, "en", "ja", "initial Japanese");
AssertMigration(new Configuration(), null, "de-DE", "de", "Dalamud fallback");
AssertMigration(new Configuration(), null, "zh", "en", "ambiguous Chinese fallback");
AssertMigration(new Configuration { Language = "en" }, ClientLanguage.Japanese, "ja", "en", "saved English");
AssertMigration(new Configuration { Language = "ja" }, ClientLanguage.English, "en", "ja", "saved Japanese");
AssertMigration(new Configuration { Language = "ko" }, ClientLanguage.English, "en", "ko", "saved Korean");
AssertMigration(new Configuration { LegacyDisplayLanguage = DisplayLanguage.English }, ClientLanguage.Japanese, "ja", "en", "legacy English");
AssertMigration(new Configuration { LegacyDisplayLanguage = DisplayLanguage.Japanese }, ClientLanguage.English, "en", "ja", "legacy Japanese");
AssertMigration(new Configuration { LegacyDisplayLanguage = (DisplayLanguage)0 }, ClientLanguage.French, "en", "fr", "legacy Auto");

foreach (string code in L.SupportedCodes)
{
    L.Configure(() => code);
    string translated = L.T("All expansions", "全拡張の進捗");
    if (string.IsNullOrWhiteSpace(translated))
        throw new InvalidOperationException($"Empty translation for {code}");
    _ = L.F("ui.routeCandidate", 1, 2, "Test", "10m");
    _ = L.F("auto.aetheryteGroundRoute", "Test", 10d, 1, 2);
}

Console.WriteLine("Localization checks passed for seven languages, resolver priority, migration, and format placeholders.");

static void AssertMigration(Configuration config, ClientLanguage? game, string? ui, string expected, string name)
{
    config.Migrate(game, ui);
    if (config.Language != expected)
        throw new InvalidOperationException($"{name}: expected {expected}, got {config.Language}");
    string saved = config.Language;
    config.Migrate(game == ClientLanguage.Japanese ? ClientLanguage.English : ClientLanguage.Japanese, "fr");
    if (config.Language != saved)
        throw new InvalidOperationException($"{name}: saved choice was overwritten");
}
