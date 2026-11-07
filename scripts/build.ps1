[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$GameDirectory,
    [string]$DotnetPath = 'dotnet',
    [switch]$IncludeHarness,
    [string]$TestDirectory
)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$gameRoot = (Resolve-Path -LiteralPath $GameDirectory).Path
$gameAssembly = Join-Path $gameRoot 'Fall of Avalon_Data/Managed/TG.Main.dll'
if ((Get-FileHash -LiteralPath $gameAssembly -Algorithm SHA256).Hash -ne '57E9FBE5659B25539FDCF1A734F0C5CDE277FEDF36EEA6FEACB237855C69EE2A') {
    Write-Warning 'Untested Mono game build. Tested on Mono 1.30.012; compilation and structural checks will determine whether this build can proceed.'
}
if ($IncludeHarness) {
    if (-not $TestDirectory -or -not [IO.Path]::IsPathRooted($TestDirectory)) { throw 'Harness builds require an absolute TestDirectory.' }
    $guard = Join-Path $TestDirectory 'test-session.active'
    if (-not (Test-Path -LiteralPath $guard) -or (Get-Content -LiteralPath $guard -Raw).Trim() -ne 'I CONFIRM THE ACTIVE GAME PROFILE IS DISPOSABLE') {
        throw 'Prepare a disposable profile and the documented confirmation file first.'
    }
}
$dotnet = (Get-Command $DotnetPath -ErrorAction Stop).Source
$env:DOTNET_ROOT = Split-Path -Parent $dotnet
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$harness = $IncludeHarness.IsPresent.ToString().ToLowerInvariant()
Push-Location -LiteralPath $repoRoot
try {
    & $dotnet build 'src/FourRings.Plugin/FourRings.Plugin.csproj' -c Release "-p:GameRoot=$gameRoot" "-p:IncludeHarness=$harness" --nologo
    if ($LASTEXITCODE) { throw 'Plugin build failed.' }
    $plugin = Join-Path $repoRoot 'src/FourRings.Plugin/bin/Release/netstandard2.1/FourRings.Plugin.dll'
    & $dotnet run --project 'tests/Static/FourRings.Tests.csproj' -c Release "-p:GameRoot=$gameRoot" -- $gameAssembly $plugin $harness
    if ($LASTEXITCODE) { throw 'Static checks failed.' }
} finally { Pop-Location }
