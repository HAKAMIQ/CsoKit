[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$InputIso,

    [string[]]$Profiles = @("game-safe", "compat", "fast", "smallest"),

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

function Get-NormalizedProfiles {
    param(
        [string[]]$ProfileNames
    )

    if ($null -eq $ProfileNames -or $ProfileNames.Count -eq 0) {
        throw "At least one profile must be specified."
    }

    $supported = @("game-safe", "compat", "fast", "smallest", "archive-smallest")
    $normalized = New-Object System.Collections.Generic.List[string]

    foreach ($profile in $ProfileNames) {
        if ([string]::IsNullOrWhiteSpace($profile)) {
            throw "Profile name is empty."
        }

        $name = $profile.Trim().ToLowerInvariant()

        if (-not ($supported -contains $name)) {
            throw "Unsupported profile '$profile'. Supported profiles: game-safe, compat, fast, smallest, archive-smallest."
        }

        if (-not $normalized.Contains($name)) {
            $normalized.Add($name)
        }
    }

    return $normalized.ToArray()
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

$ProfileList = Get-NormalizedProfiles -ProfileNames $Profiles
$DirectoryPath = $InputItem.DirectoryName
$WorkRoot = Join-Path $DirectoryPath (".csokit-profiles-" + [System.Guid]::NewGuid().ToString("N"))
$OriginalHash = Get-Sha256Hex -Path $InputIsoPath
$OriginalSize = (Get-Item -LiteralPath $InputIsoPath).Length
$Results = New-Object System.Collections.Generic.List[object]
$success = $false

Write-Host "CsoKit Profile Roundtrip Matrix"
Write-Host "Input:     $InputIsoPath"
Write-Host "Profiles:  $($ProfileList -join ', ')"
Write-Host "Artifacts: $(if ($KeepArtifacts) { 'kept' } else { 'deleted on success' })"
Write-Host "SHA256:    $OriginalHash"

try {
    New-Item -ItemType Directory -Force -Path $WorkRoot | Out-Null

    foreach ($profile in $ProfileList) {
        $profileRoot = Join-Path $WorkRoot $profile
        New-Item -ItemType Directory -Force -Path $profileRoot | Out-Null
        $CsoArtifactPath = Join-Path $profileRoot "out.cso"
        $RestoredIsoPath = Join-Path $profileRoot "back.iso"
        Assert-CsoKitOutputFileName -Path $CsoArtifactPath -Context "Profile matrix CSO"
        Assert-CsoKitOutputFileName -Path $RestoredIsoPath -Context "Profile matrix restored ISO"

        Write-Host ""
        Write-Host "Profile: $profile"
        Write-Host "CSO output: $CsoArtifactPath"
        Write-Host "ISO output: $RestoredIsoPath"

        $compressArgs = @("compress", $InputIsoPath, "-o", $CsoArtifactPath, "--profile", $profile, "--deep-verify")
        $verifyArgs = @("verify", $CsoArtifactPath, "--deep", "--sha256")
        $decompressArgs = @("decompress", $CsoArtifactPath, "-o", $RestoredIsoPath)

        if ($Quiet) {
            $compressArgs += "--quiet"
            $verifyArgs += "--json"
            $decompressArgs += "--quiet"
        }

        Invoke-CsoKit -StepName "Compress ISO to CSO ($profile)" -CommandArguments $compressArgs

        if (-not (Test-Path -LiteralPath $CsoArtifactPath)) {
            throw "Compression completed but CSO artifact was not found: $CsoArtifactPath"
        }

        Invoke-CsoKit -StepName "Deep verify CSO ($profile)" -CommandArguments $verifyArgs

        Invoke-CsoKit -StepName "Decompress CSO to ISO ($profile)" -CommandArguments $decompressArgs

        if (-not (Test-Path -LiteralPath $RestoredIsoPath)) {
            throw "Decompression completed but restored ISO was not found: $RestoredIsoPath"
        }

        $restoredHash = Get-Sha256Hex -Path $RestoredIsoPath
        $restoredSize = (Get-Item -LiteralPath $RestoredIsoPath).Length
        $csoSize = (Get-Item -LiteralPath $CsoArtifactPath).Length
        $ratio = 0

        if ($OriginalSize -gt 0) {
            $ratio = [double]$csoSize / [double]$OriginalSize
        }

        if ($OriginalSize -ne $restoredSize) {
            throw "Roundtrip size mismatch for profile '$profile'. Original=$OriginalSize Restored=$restoredSize"
        }

        if (-not [string]::Equals($OriginalHash, $restoredHash, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Roundtrip SHA256 mismatch for profile '$profile'."
        }

        $Results.Add([pscustomobject]@{
            Profile = $profile
            CsoBytes = $csoSize
            Ratio = ("{0:N2}%" -f ($ratio * 100))
            IsoBytes = $restoredSize
            Status = "PASS"
        }) | Out-Null
    }

    Write-Host ""
    Write-Host "Profile roundtrip matrix result"
    Write-Host "-------------------------------"
    $Results | Format-Table -AutoSize | Out-String | Write-Host

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
