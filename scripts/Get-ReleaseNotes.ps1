[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [string]$OutputPath,

    [string]$NotesPath = ""
)

# Writes only the "# CsoKit <Version>" section of RELEASE_NOTES.md, so a GitHub
# release page shows its own notes instead of the whole history.

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($NotesPath)) {
    $NotesPath = Join-Path (Join-Path $PSScriptRoot "..") "RELEASE_NOTES.md"
}

if (-not (Test-Path -LiteralPath $NotesPath -PathType Leaf)) {
    throw "Release notes file was not found: $NotesPath"
}

$text = Get-Content -LiteralPath $NotesPath -Raw
$pattern = "(?ms)^# CsoKit $([regex]::Escape($Version))[ \t]*\r?$.*?(?=^# CsoKit |\z)"
$match = [regex]::Match($text, $pattern)

if (-not $match.Success) {
    throw "RELEASE_NOTES.md has no '# CsoKit $Version' section."
}

$section = $match.Value.TrimEnd() + [Environment]::NewLine

if ($section -match "GATE_RESULTS_PLACEHOLDER|Final release gate: pending") {
    throw "The $Version release notes still have no recorded release gate results."
}

$directory = Split-Path -Parent $OutputPath
if (-not [string]::IsNullOrWhiteSpace($directory)) {
    New-Item -ItemType Directory -Force -Path $directory | Out-Null
}

[System.IO.File]::WriteAllText($OutputPath, $section, [System.Text.UTF8Encoding]::new($false))
Write-Host "Release notes for $Version written to $OutputPath"
