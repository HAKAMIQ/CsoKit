[CmdletBinding()]
param(
    [string]$Root,
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$KeepArtifacts,
    [switch]$Json
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-GateJson {
    param([object]$Value)
    if ($Json) {
        $Value | ConvertTo-Json -Depth 8
    }
}

if ([string]::IsNullOrWhiteSpace($Root)) {
    $message = 'Run-RealLocalCorpusGate is optional. Provide -Root <local corpus folder> to execute it; release gates do not require this corpus.'
    if ($Json) {
        Write-GateJson ([ordered]@{
            schemaVersion = 1
            command = 'real-local-corpus-gate'
            success = $true
            skipped = $true
            message = $message
        })
    }
    else {
        Write-Host $message
    }
    exit 0
}

$resolvedRoot = (Resolve-Path -LiteralPath $Root -ErrorAction Stop).ProviderPath
if (-not (Test-Path -LiteralPath $resolvedRoot -PathType Container)) {
    throw "Root is not a directory: $Root"
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $repoRoot "src/CsoKit.Cli/bin/$Configuration/net10.0/csokit.exe"
$dll = Join-Path $repoRoot "src/CsoKit.Cli/bin/$Configuration/net10.0/CsoKit.Cli.dll"

if (Test-Path -LiteralPath $exe -PathType Leaf) {
    $tool = @($exe)
}
elseif (Test-Path -LiteralPath $dll -PathType Leaf) {
    $tool = @('dotnet', $dll)
}
else {
    Write-Host "CLI binary was not found. Building $Configuration first..."
    & dotnet build (Join-Path $repoRoot 'CsoKit.slnx') -c $Configuration
    if (Test-Path -LiteralPath $exe -PathType Leaf) {
        $tool = @($exe)
    }
    else {
        $tool = @('dotnet', $dll)
    }
}

$artifactRoot = Join-Path ([IO.Path]::GetTempPath()) ("CsoKit_RealCorpus_" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $artifactRoot | Out-Null

$extensions = @('.iso', '.cso', '.zso', '.dax', '.cso2')
$files = Get-ChildItem -LiteralPath $resolvedRoot -File -Recurse |
    Where-Object { $extensions -contains $_.Extension.ToLowerInvariant() } |
    Sort-Object FullName

$results = New-Object System.Collections.Generic.List[object]
$success = $true

function Invoke-CsoTool {
    param([string[]]$Arguments)
    $prefix = if ($tool.Count -gt 1) { @($tool[1..($tool.Count - 1)]) } else { @() }
    & $tool[0] @($prefix + $Arguments)
    if ($LASTEXITCODE -ne 0) {
        throw "csokit failed with exit code $LASTEXITCODE: $($Arguments -join ' ')"
    }
}

try {
    foreach ($file in $files) {
        $caseId = [Guid]::NewGuid().ToString('N')
        $caseDir = Join-Path $artifactRoot $caseId
        New-Item -ItemType Directory -Path $caseDir | Out-Null

        $detectJson = Join-Path $caseDir 'detect.json'
        $prefix = if ($tool.Count -gt 1) { @($tool[1..($tool.Count - 1)]) } else { @() }
        & $tool[0] @($prefix + @('detect', $file.FullName, '--json')) | Tee-Object -FilePath $detectJson | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "detect failed for $($file.FullName)" }

        $detect = Get-Content -LiteralPath $detectJson -Raw | ConvertFrom-Json
        $format = [string]$detect.format

        if ($format -eq 'RawIso') {
            Invoke-CsoTool @('analyze', $file.FullName, '--psp', '--json') | Out-Null
        }
        elseif ($format -in @('Cso1', 'Cso2', 'Zso', 'Dax')) {
            Invoke-CsoTool @('verify', $file.FullName, '--deep', '--json') | Out-Null
        }
        else {
            $results.Add([ordered]@{ input = $file.FullName; format = $format; skipped = $true; reason = 'Unsupported or unknown format.' })
            continue
        }

        $fixed = Join-Path $caseDir 'fixed.cso'
        Invoke-CsoTool @('repair', $file.FullName, '-o', $fixed, '--deep-verify', '--json') | Out-Null
        Invoke-CsoTool @('verify', $fixed, '--deep', '--json') | Out-Null

        $restored = Join-Path $caseDir 'restored.iso'
        Invoke-CsoTool @('decompress', $fixed, '-o', $restored, '--force', '--json') | Out-Null

        $hashMatches = $null
        if ($format -eq 'RawIso') {
            $sourceHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $file.FullName).Hash
            $restoredHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $restored).Hash
            $hashMatches = $sourceHash -eq $restoredHash
            if (-not $hashMatches) { throw "Restored ISO hash mismatch for $($file.FullName)" }
        }

        $results.Add([ordered]@{
            input = $file.FullName
            format = $format
            repaired = $true
            verified = $true
            hashMatches = $hashMatches
        })
    }
}
catch {
    $success = $false
    if (-not $Json) { Write-Error $_ }
    $results.Add([ordered]@{ error = $_.Exception.Message })
}
finally {
    if (-not $KeepArtifacts) {
        Remove-Item -LiteralPath $artifactRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

if ($Json) {
    Write-GateJson ([ordered]@{
        schemaVersion = 1
        command = 'real-local-corpus-gate'
        success = $success
        skipped = $false
        root = [string]$resolvedRoot
        artifacts = $(if ($KeepArtifacts) { $artifactRoot } else { $null })
        filesChecked = $files.Count
        results = $results
    })
}
else {
    if ($success) {
        Write-Host "Real local corpus gate PASS. Files checked: $($files.Count)"
        if ($KeepArtifacts) { Write-Host "Artifacts: $artifactRoot" }
    }
}

if ($success) { exit 0 } else { exit 1 }
