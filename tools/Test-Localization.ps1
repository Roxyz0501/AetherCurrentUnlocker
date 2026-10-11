$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$catalogPath = Join-Path $root 'AetherCurrentUnlocker\LocalizationCatalog.cs'
$catalog = Get-Content -LiteralPath $catalogPath -Raw
$sources = Get-ChildItem (Join-Path $root 'AetherCurrentUnlocker') -Recurse -Filter '*.cs' |
    Where-Object FullName -ne $catalogPath |
    ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
$source = $sources -join "`n"

$staticKeys = [regex]::Matches($source, 'L\.T\(\s*"((?:\\.|[^"\\])*)"', 'Singleline') |
    ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique
$missingStatic = @($staticKeys | Where-Object { $catalog -notmatch [regex]::Escape('A("' + $_ + '"') })
if ($missingStatic.Count -gt 0) { throw "Missing static translations: $($missingStatic -join ', ')" }

$formatKeys = [regex]::Matches($source, 'L\.F\("([^"]+)"') |
    ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique
$missingFormats = @($formatKeys | Where-Object { $catalog -notmatch [regex]::Escape('A("' + $_ + '"') })
if ($missingFormats.Count -gt 0) { throw "Missing format translations: $($missingFormats -join ', ')" }

if ([regex]::IsMatch($source, 'L\.T\(\$')) { throw 'Interpolated L.T call remains; use a shared L.F format key.' }
$languageSource = Get-Content -LiteralPath (Join-Path $root 'AetherCurrentUnlocker\Localization.cs') -Raw
foreach ($sample in @('日本語', 'Français', '한국어', '简体中文', '繁體中文')) {
    if (-not ($catalog + $languageSource).Contains($sample)) { throw "Required glyph sample is missing: $sample" }
}

$sharedTabs = Join-Path $root 'AetherCurrentUnlocker\Windows\StandardPluginTabs.cs'
$canonicalTabs = Join-Path $root '..\templates\Dalamud\StandardPluginTabs.cs'
if (Test-Path -LiteralPath $canonicalTabs) {
    $pluginHash = (Get-FileHash -LiteralPath $sharedTabs -Algorithm SHA256).Hash
    $canonicalHash = (Get-FileHash -LiteralPath $canonicalTabs -Algorithm SHA256).Hash
    if ($pluginHash -ne $canonicalHash) { throw 'StandardPluginTabs.cs differs from the canonical workspace template.' }
}
$windowSource = Get-Content -LiteralPath (Join-Path $root 'AetherCurrentUnlocker\Windows\MainWindow.cs') -Raw
foreach ($required in @('###LanguageSettings', '###Support', 'StandardPluginTabs.DrawLanguageSelector', 'StandardPluginTabs.DrawSupportContent')) {
    if (-not $windowSource.Contains($required)) { throw "Required shared-tab integration is missing: $required" }
}

dotnet run --project (Join-Path $root 'AetherCurrentUnlocker.LocalizationTests\AetherCurrentUnlocker.LocalizationTests.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw "Localization test executable failed with exit code $LASTEXITCODE." }
Write-Output "Static coverage passed: $($staticKeys.Count) literals and $($formatKeys.Count) format keys."
