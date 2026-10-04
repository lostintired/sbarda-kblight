# Checks that the release tag matches <Version> in src/KbLight.csproj and writes the release notes.
# Notes: .github/release-notes/v<version>.md if it exists (English, for users), otherwise the version's
# section of docs/CHANGELOG.md; then the runtime requirement and the SHA256 of the exe.
# Usage: release-notes.ps1 -Tag v1.2.0 -Exe publish/KbLight.exe -Out notes.md
param(
    [Parameter(Mandatory)] [string] $Tag,
    [Parameter(Mandatory)] [string] $Exe,
    [Parameter(Mandatory)] [string] $Out
)
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '../..')

$version = ([xml](Get-Content (Join-Path $root 'src/KbLight.csproj') -Raw)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if ($Tag -ne "v$version") { throw "tag $Tag does not match <Version>$version</Version> in src/KbLight.csproj" }

$custom = Join-Path $root ".github/release-notes/$Tag.md"
if (Test-Path $custom) {
    $body = (Get-Content $custom -Raw).Trim()
} else {
    $lines = Get-Content (Join-Path $root 'docs/CHANGELOG.md')
    $start = [array]::FindIndex($lines, [Predicate[string]] { param($l) $l -match "^## \[$([regex]::Escape($version))\]" })
    if ($start -lt 0) { throw "docs/CHANGELOG.md has no section ## [$version]" }
    $end = [array]::FindIndex($lines, $start + 1, [Predicate[string]] { param($l) $l -match '^## \[' })
    if ($end -lt 0) { $end = $lines.Count }
    $body = ($lines[($start + 1)..($end - 1)] -join "`n").Trim()
}

$hash = (Get-FileHash $Exe -Algorithm SHA256).Hash.ToLowerInvariant()
@"
$body

**Requires** [.NET Desktop Runtime 10 (x64)](https://dotnet.microsoft.com/download/dotnet/10.0). The exe is not signed — SmartScreen may ask: *More info → Run anyway*. Built by GitHub Actions from this tag.

``````
SHA256  $hash  KbLight.exe
``````
"@ | Set-Content $Out -Encoding utf8NoBOM
Write-Host "release notes for $Tag ($version), SHA256 $hash"
