#Requires -Version 7.2
<#
.SYNOPSIS
    Builds the Salt.er release files for one architecture.

.DESCRIPTION
    Publishes the app self-contained, packs it with Velopack (Setup.exe and the update feed for the channel
    win-<Arch>), zips the published folder as the portable build, and writes SHA256SUMS.txt for the result.

    Output: artifacts/releases/<Arch>. Both artifacts/publish/<Arch> and artifacts/releases/<Arch> are emptied first.
    The Velopack CLI (vpk) is installed into artifacts/tools, at the version of the Velopack package the app uses.
    Windows only.

.PARAMETER Version
    The release version, a semantic version such as 1.2.0 or 1.3.0-rc.1, without a leading "v". Velopack requires
    at least 0.0.1 (a pre-release such as 0.0.1-ci.42 counts), so 0.0.0 versions are rejected.

.PARAMETER Arch
    The target architecture: x64 or arm64.

.PARAMETER Configuration
    The build configuration. Defaults to Release.

.EXAMPLE
    pwsh build/pack.ps1 -Version 1.0.0 -Arch x64
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(-[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*)?$')]
    [string] $Version,

    [Parameter(Mandatory)]
    [ValidateSet('x64', 'arm64')]
    [string] $Arch,

    [string] $Configuration = 'Release'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-Native {
    param(
        [Parameter(Mandatory)] [string] $FilePath,
        [Parameter(Mandatory)] [string[]] $Arguments
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$FilePath $($Arguments[0]) failed with exit code $LASTEXITCODE."
    }
}

function Reset-Directory([string] $Path) {
    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Recurse -Force
    }

    New-Item -ItemType Directory -Path $Path | Out-Null
}

# Velopack compares the version without its pre-release label against 0.0.1.
if ($Version -match '^0\.0\.0(-|$)') {
    throw "Version $Version is below 0.0.1, the lowest version Velopack accepts."
}

if (-not $IsWindows) {
    throw 'The app is a WinUI 3 application and can only be packaged on Windows.'
}

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src/Salter.App/Salter.App.csproj'
$icon = Join-Path $root 'src/Salter.App/Assets/AppIcon.ico'
$runtime = "win-$Arch"
# The same platform names as the solution and CI use.
$platform = @{ x64 = 'x64'; arm64 = 'ARM64' }[$Arch]
$publishDir = Join-Path $root "artifacts/publish/$Arch"
$outputDir = Join-Path $root "artifacts/releases/$Arch"

# vpk must match the Velopack library in the app, so take its version from the central package versions.
$packages = [xml](Get-Content -LiteralPath (Join-Path $root 'Directory.Packages.props') -Raw)
$velopack = $packages.SelectSingleNode("/Project/ItemGroup/PackageVersion[@Include='Velopack']")
if ($null -eq $velopack) {
    throw 'Directory.Packages.props has no Velopack version.'
}

$vpkVersion = $velopack.GetAttribute('Version')
$toolDir = Join-Path $root "artifacts/tools/vpk-$vpkVersion"
$vpk = Join-Path $toolDir 'vpk.exe'
if (-not (Test-Path -LiteralPath $vpk)) {
    Invoke-Native dotnet @('tool', 'install', 'vpk', '--version', $vpkVersion, '--tool-path', $toolDir)
}

Reset-Directory $publishDir
Reset-Directory $outputDir

Write-Host "Publishing Salt.er $Version for $runtime"
Invoke-Native dotnet @(
    'publish', $project,
    '--configuration', $Configuration,
    '--runtime', $runtime,
    '--self-contained',
    "-p:Platform=$platform",
    "-p:Version=$Version",
    '--output', $publishDir
)

# Without the project PRI no XAML page can load, and the app fails at startup.
if (-not (Test-Path -LiteralPath (Join-Path $publishDir 'Salter.App.pri'))) {
    throw "The published app in $publishDir has no Salter.App.pri."
}

# Zipped before packing, so the portable build holds exactly the published files.
Add-Type -AssemblyName System.IO.Compression.ZipFile
$portableZip = Join-Path $outputDir "Salt.er-$Version-$runtime-portable.zip"
Write-Host "Creating $portableZip"
[System.IO.Compression.ZipFile]::CreateFromDirectory(
    $publishDir, $portableZip, [System.IO.Compression.CompressionLevel]::Optimal, $false)

Write-Host 'Packing with Velopack'
# Velopack's own portable build is skipped: the portable ZIP above is the portable build.
Invoke-Native $vpk @(
    'pack',
    '--packId', 'Salt.er',
    '--packVersion', $Version,
    '--packDir', $publishDir,
    '--mainExe', 'Salter.App.exe',
    '--icon', $icon,
    '--runtime', $runtime,
    '--channel', $runtime,
    '--outputDir', $outputDir,
    '--noPortable'
)

$sumsFile = Join-Path $outputDir 'SHA256SUMS.txt'
$lines = Get-ChildItem -LiteralPath $outputDir -File |
    Sort-Object -Property Name |
    ForEach-Object {
        $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        # The format of sha256sum, so `sha256sum -c SHA256SUMS.txt` verifies the files.
        "$hash  $($_.Name)"
    }
[System.IO.File]::WriteAllText($sumsFile, (($lines -join "`n") + "`n"))

Write-Host "Release files in ${outputDir}:"
Get-ChildItem -LiteralPath $outputDir -File | ForEach-Object { Write-Host "  $($_.Name)" }
