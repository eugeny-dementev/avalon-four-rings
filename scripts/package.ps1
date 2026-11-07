[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$GameDirectory,
    [string]$DotnetPath = 'dotnet',
    [string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repoRoot 'artifacts' }
$output = [IO.Path]::GetFullPath($OutputDirectory)
$archive = Join-Path $output 'FourRings-1.0.1-Mono.zip'
$checksum = Join-Path $output 'SHA256SUMS.txt'
if ((Test-Path -LiteralPath $archive) -or (Test-Path -LiteralPath $checksum)) { throw 'Release output already exists. Select a new output directory.' }
& (Join-Path $PSScriptRoot 'build.ps1') -GameDirectory $GameDirectory -DotnetPath $DotnetPath
[IO.Directory]::CreateDirectory($output) | Out-Null
Add-Type -AssemblyName System.IO.Compression
$files = [ordered]@{
    'BepInEx/patchers/FourRings/FourRings.Patcher.dll' = 'src/FourRings.Patcher/bin/Release/netstandard2.1/FourRings.Patcher.dll'
    'BepInEx/plugins/FourRings/FourRings.Plugin.dll' = 'src/FourRings.Plugin/bin/Release/netstandard2.1/FourRings.Plugin.dll'
    'README.txt' = 'docs/INSTALL.txt'
    'LICENSE' = 'LICENSE'
    'CHANGELOG.txt' = 'CHANGELOG.md'
}
$stream = [IO.File]::Open($archive, [IO.FileMode]::CreateNew)
try {
    $zip = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create, $true)
    try {
        foreach ($name in $files.Keys) {
            $entry = $zip.CreateEntry($name, [IO.Compression.CompressionLevel]::Optimal)
            # Each release needs a distinct timestamp so BepInEx refreshes its cached plugin metadata.
            $entry.LastWriteTime = [DateTimeOffset]::new(2026, 11, 7, 0, 0, 2, [TimeSpan]::Zero)
            $entryStream = $entry.Open()
            try {
                $bytes = [IO.File]::ReadAllBytes((Join-Path $repoRoot $files[$name]))
                $entryStream.Write($bytes, 0, $bytes.Length)
            } finally { $entryStream.Dispose() }
        }
    } finally { $zip.Dispose() }
} finally { $stream.Dispose() }
$check = [IO.Compression.ZipFile]::OpenRead($archive)
try {
    $names = @($check.Entries.FullName)
    if ($names.Count -ne $files.Count -or (Compare-Object ($names | Sort-Object) ($files.Keys | Sort-Object))) { throw 'Archive contents differ from the release allowlist.' }
    foreach ($entry in $check.Entries) {
        $entryStream = $entry.Open()
        $sha = [Security.Cryptography.SHA256]::Create()
        try { $entryHash = [Convert]::ToHexString($sha.ComputeHash($entryStream)) } finally { $sha.Dispose(); $entryStream.Dispose() }
        if ($entryHash -ne (Get-FileHash -LiteralPath (Join-Path $repoRoot $files[$entry.FullName]) -Algorithm SHA256).Hash) { throw "Archive file differs: $($entry.FullName)" }
    }
} finally { $check.Dispose() }
$hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText($checksum, "$hash  FourRings-1.0.1-Mono.zip`n", [Text.UTF8Encoding]::new($false))
"Validated release archive: $archive"
"SHA-256: $hash"
