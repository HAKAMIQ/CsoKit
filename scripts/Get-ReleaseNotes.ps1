[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [string]$OutputPath,

    [string]$NotesPath = ""
)

# Writes the body of the "# CsoKit <Version>" section of RELEASE_NOTES.md (without
# the heading, which the release title already shows), so a GitHub release page
# carries only its own user-facing notes. Refuses to run until the release gate
# results are recorded in docs/verification/<Version>.md.

$ErrorActionPreference = "Stop"

$repoRoot = Join-Path $PSScriptRoot ".."

if ([string]::IsNullOrWhiteSpace($NotesPath)) {
    $NotesPath = Join-Path $repoRoot "RELEASE_NOTES.md"
}

$verificationPath = Join-Path (Join-Path (Join-Path $repoRoot "docs") "verification") "$Version.md"

if (-not (Test-Path -LiteralPath $verificationPath -PathType Leaf)) {
    throw "Release gate results for $Version are not recorded in docs/verification/$Version.md."
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

$section = ($match.Value -replace "^# CsoKit [^\r\n]*\r?\n", "").Trim() + [Environment]::NewLine

$directory = Split-Path -Parent $OutputPath
if (-not [string]::IsNullOrWhiteSpace($directory)) {
    New-Item -ItemType Directory -Force -Path $directory | Out-Null
}

[System.IO.File]::WriteAllText($OutputPath, $section, [System.Text.UTF8Encoding]::new($false))
Write-Host "Release notes for $Version written to $OutputPath"
