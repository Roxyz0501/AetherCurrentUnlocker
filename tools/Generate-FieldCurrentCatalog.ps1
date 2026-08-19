param(
    [Parameter(Mandatory = $true)] [string] $QuestPaths,
    [Parameter(Mandatory = $true)] [string] $OutputFile
)

$ErrorActionPreference = 'Stop'
$items = [System.Collections.Generic.List[object]]::new()

Get-ChildItem -LiteralPath $QuestPaths -Recurse -Filter '*.json' | ForEach-Object {
    try {
        $json = Get-Content -LiteralPath $_.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
        foreach ($sequence in @($json.QuestSequence)) {
            foreach ($step in @($sequence.Steps)) {
                if ($step.InteractionType -eq 'AttuneAetherCurrent' -and
                    $null -ne $step.AetherCurrentId -and $null -ne $step.Position) {
                    $items.Add([pscustomobject]@{
                        TerritoryId = [uint32]$step.TerritoryId
                        CurrentId   = [uint32]$step.AetherCurrentId
                        DataId      = [uint32]$step.DataId
                        X           = [single]$step.Position.X
                        Y           = [single]$step.Position.Y
                        Z           = [single]$step.Position.Z
                    })
                }
            }
        }
    }
    catch {
        Write-Warning "Skipped $($_.FullName): $($_.Exception.Message)"
    }
}

$unique = $items | Sort-Object CurrentId -Unique
$culture = [System.Globalization.CultureInfo]::InvariantCulture
$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add('// Generated from the official Questionable QuestPaths repository.')
$lines.Add('// Regenerate with tools/Generate-FieldCurrentCatalog.ps1 when upstream data changes.')
$lines.Add('using System.Numerics;')
$lines.Add('using AetherCurrentUnlocker.Models;')
$lines.Add('')
$lines.Add('namespace AetherCurrentUnlocker.Data;')
$lines.Add('')
$lines.Add('internal static class FieldCurrentCatalog')
$lines.Add('{')
$lines.Add('    public static IReadOnlyList<FieldCurrent> All { get; } =')
$lines.Add('    [')
foreach ($item in $unique) {
    $x = $item.X.ToString('R', $culture)
    $y = $item.Y.ToString('R', $culture)
    $z = $item.Z.ToString('R', $culture)
    $lines.Add("        new($($item.TerritoryId)u, $($item.CurrentId)u, $($item.DataId)u, new Vector3(${x}f, ${y}f, ${z}f)),")
}
$lines.Add('    ];')
$lines.Add('}')

[System.IO.File]::WriteAllLines($OutputFile, $lines, [System.Text.UTF8Encoding]::new($false))
Write-Host "Generated $($unique.Count) unique field-current locations."
