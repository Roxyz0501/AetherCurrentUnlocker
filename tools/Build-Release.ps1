param(
    [ValidateSet('Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$pluginRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$project = Join-Path $pluginRoot 'AetherCurrentUnlocker\AetherCurrentUnlocker.csproj'
$outputDirectory = Join-Path $pluginRoot 'AetherCurrentUnlocker\bin\Release\AetherCurrentUnlocker'
$latestZip = Join-Path $outputDirectory 'latest.zip'
$artifactDirectory = Join-Path $pluginRoot 'artifacts'

[xml]$projectXml = Get-Content -LiteralPath $project -Raw
$version = [string]$projectXml.Project.PropertyGroup.Version
$assemblyName = [string]$projectXml.Project.PropertyGroup.AssemblyName
if ([string]::IsNullOrWhiteSpace($version) -or [string]::IsNullOrWhiteSpace($assemblyName)) {
    throw 'Version or AssemblyName is missing from the project file.'
}

dotnet restore $project --locked-mode
if ($LASTEXITCODE -ne 0) {
    throw "dotnet restore --locked-mode failed with exit code $LASTEXITCODE."
}

dotnet build $project -c $Configuration --no-restore
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE."
}
if (-not (Test-Path -LiteralPath $latestZip -PathType Leaf)) {
    throw "Dalamud package was not generated: $latestZip"
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead($latestZip)
try {
    $expected = @("$assemblyName.deps.json", "$assemblyName.dll", "$assemblyName.json")
    $actual = @($archive.Entries | Where-Object { -not $_.FullName.EndsWith('/') } | ForEach-Object FullName | Sort-Object)
    $difference = Compare-Object ($expected | Sort-Object) $actual
    if ($difference) {
        $entries = $actual -join ', '
        throw "Unexpected Release ZIP contents: $entries"
    }
} finally {
    $archive.Dispose()
}

New-Item -ItemType Directory -Force -Path $artifactDirectory | Out-Null
$artifact = Join-Path $artifactDirectory "$assemblyName-$version.zip"
Copy-Item -LiteralPath $latestZip -Destination $artifact -Force
$hash = Get-FileHash -LiteralPath $artifact -Algorithm SHA256
Write-Output "Artifact: $artifact"
Write-Output "SHA256: $($hash.Hash)"
