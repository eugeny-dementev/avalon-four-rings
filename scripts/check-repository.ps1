$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$files = @(git -C $repoRoot ls-files)
if ($LASTEXITCODE -or -not $files.Count) { throw 'Run this check from a Git checkout with tracked files.' }
$required = @('README.md','LICENSE','CHANGELOG.md','docs/INSTALL.txt','docs/BUILDING.md','docs/TESTING.md','docs/DEPENDENCIES.md','scripts/build.ps1','scripts/package.ps1')
foreach ($name in $required) { if ($name -notin $files) { throw "Missing public file: $name" } }
foreach ($name in $files) {
    if ($name -match '(^|/)(bin|obj|artifacts|Saved|decompiled)/|\.(dll|exe|pdb|zip|7z|log|data)$') { throw "Private or generated artifact tracked: $name" }
    $path = Join-Path $repoRoot $name
    if ($name -match '\.(md|txt|cs|csproj|props|ps1|json|yml|yaml)$') {
        $content = [IO.File]::ReadAllText($path)
        if ($content -match '[A-Za-z]:[\\/]+Users[\\/]+[^\s/\\]+|gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{40,}') { throw "Personal path or credential pattern in $name" }
    }
    if ($name.EndsWith('.md')) {
        foreach ($match in [regex]::Matches($content, '\]\(([^)\s]+)\)')) {
            $target = $match.Groups[1].Value
            if ($target -match '^(https?://|mailto:|#)') { continue }
            $target = ($target -split '#')[0]
            if (-not (Test-Path -LiteralPath (Join-Path (Split-Path -Parent $path) $target))) { throw "Broken relative link in ${name}: $target" }
        }
    }
}
'PASS: public repository hygiene and relative documentation links. Game-dependent tests run separately.'
