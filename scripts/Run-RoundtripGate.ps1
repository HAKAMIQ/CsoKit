[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$InputIso,

    [switch]$KeepArtifacts,

    [switch]$Quiet
)

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "OutputFileNamePolicy.ps1")

function Get-ResolvedFilePath {
    param(
        [string]$Path,
        [string]$Description
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        throw "$Description path is empty."
    }

    $resolved = Resolve-Path -LiteralPath $Path -ErrorAction SilentlyContinue

    if (-not $resolved) {
        throw "$Description was not found: $Path"
    }

    $item = Get-Item -LiteralPath $resolved.Path -ErrorAction Stop

    if ($item.PSIsContainer) {
        throw "$Description must be a file, not a directory: $($item.FullName)"
    }

    return $item.FullName
}

function Invoke-CsoKit {
    param(
        [string]$StepName,
        [string[]]$CommandArguments
    )

    Write-Host ""
    Write-Host "[$StepName]"

    $dotnetArguments = @(
        "run",
        "--project",
        $CliProject,
        "--no-restore",
        "--"
    ) + $CommandArguments

    & dotnet @dotnetArguments

    if ($LASTEXITCODE -ne 0) {
        throw "$StepName failed with exit code $LASTEXITCODE."
    }
}

function Get-Sha256Hex {
    param(
        [string]$Path
    )

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToUpperInvariant()
}

$RepoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
$CliProject = Join-Path $RepoRoot "src\CsoKit.Cli\CsoKit.Cli.csproj"

if (-not (Test-Path -LiteralPath $CliProject)) {
    throw "CLI project was not found: $CliProject"
}

$InputIsoPath = Get-ResolvedFilePath -Path $InputIso -Description "Input ISO"
$InputItem = Get-Item -LiteralPath $InputIsoPath -ErrorAction Stop

if (-not [string]::Equals($InputItem.Extension, ".iso", [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Input file must use the .iso extension: $InputIsoPath"
}

$DirectoryPath = $InputItem.DirectoryName
$WorkRoot = Join-Path $DirectoryPath (".csokit-roundtrip-" + [System.Guid]::NewGuid().ToString("N"))
$CsoArtifactPath = Join-Path $WorkRoot "out.cso"
$RestoredIsoPath = Join-Path $WorkRoot "back.iso"
Assert-CsoKitOutputFileName -Path $CsoArtifactPath -Context "Round-trip gate CSO"
Assert-CsoKitOutputFileName -Path $RestoredIsoPath -Context "Round-trip gate restored ISO"

$compressArgs = @("compress", $InputIsoPath, "-o", $CsoArtifactPath)
$verifyArgs = @("verify", $CsoArtifactPath, "--deep", "--sha256")
$decompressArgs = @("decompress", $CsoArtifactPath, "-o", $RestoredIsoPath)

if ($Quiet) {
    $compressArgs += "--quiet"
    $decompressArgs += "--quiet"
}

Write-Host "CsoKit Roundtrip Gate"
Write-Host "Input:       $InputIsoPath"
Write-Host "CSO output:  $CsoArtifactPath"
Write-Host "ISO output:  $RestoredIsoPath"
Write-Host "Artifacts:   $(if ($KeepArtifacts) { 'kept' } else { 'deleted on success' })"

$success = $false

try {
    New-Item -ItemType Directory -Force -Path $WorkRoot | Out-Null
    $originalHash = Get-Sha256Hex -Path $InputIsoPath

    Invoke-CsoKit -StepName "Compress ISO to CSO" -CommandArguments $compressArgs

    if (-not (Test-Path -LiteralPath $CsoArtifactPath)) {
        throw "Compression completed but CSO artifact was not found: $CsoArtifactPath"
    }

    Invoke-CsoKit -StepName "Deep verify CSO" -CommandArguments $verifyArgs

    Invoke-CsoKit -StepName "Decompress CSO to ISO" -CommandArguments $decompressArgs

    if (-not (Test-Path -LiteralPath $RestoredIsoPath)) {
        throw "Decompression completed but restored ISO was not found: $RestoredIsoPath"
    }

    $restoredHash = Get-Sha256Hex -Path $RestoredIsoPath

    $originalSize = (Get-Item -LiteralPath $InputIsoPath).Length
    $restoredSize = (Get-Item -LiteralPath $RestoredIsoPath).Length
    $csoSize = (Get-Item -LiteralPath $CsoArtifactPath).Length

    Write-Host ""
    Write-Host "Roundtrip result"
    Write-Host "----------------"
    Write-Host "Original SHA256: $originalHash"
    Write-Host "Restored SHA256: $restoredHash"
    Write-Host "Original bytes:  $originalSize"
    Write-Host "Restored bytes:  $restoredSize"
    Write-Host "CSO bytes:       $csoSize"

    if ($originalSize -ne $restoredSize) {
        throw "Roundtrip size mismatch. Original=$originalSize Restored=$restoredSize"
    }

    if (-not [string]::Equals($originalHash, $restoredHash, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Roundtrip SHA256 mismatch."
    }

    $success = $true
    Write-Host "Status: PASS"
}
finally {
    if ($success -and -not $KeepArtifacts) {
        Remove-Item -LiteralPath $WorkRoot -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "Artifacts removed."
    }
    elseif ($success) {
        Write-Host "Artifacts kept: $WorkRoot"
    }
    else {
        Write-Host "Status: FAILED"
        Write-Host "Artifacts were kept for inspection: $WorkRoot"
    }
}
