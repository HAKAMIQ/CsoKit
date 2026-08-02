[CmdletBinding()]
param(
    [string]$Version = "",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

function Invoke-NativeInfoCheck {
    param(
        [string]$ExecutablePath,
        [string]$Context
    )

    $nativeInfoOutput = ((& $ExecutablePath native-info) | Out-String).Trim()

    if ($LASTEXITCODE -ne 0) {
        throw "native-info command failed for $Context."
    }

    if ($nativeInfoOutput -notmatch "Backend:\s+native") {
        Write-Host $nativeInfoOutput
        throw "native-info did not report native backend for $Context."
    }

    if ($nativeInfoOutput -notmatch "Native available:\s+True") {
        Write-Host $nativeInfoOutput
        throw "native-info did not report native availability for $Context."
    }

    foreach ($requiredCapability in @(
        "Native zlib:\s+available",
        "Native libdeflate:\s+available",
        "Native Zopfli:\s+available",
        "LZ4 decode:\s+available"
    )) {
        if ($nativeInfoOutput -notmatch $requiredCapability) {
            Write-Host $nativeInfoOutput
            throw "native-info did not report required codec capability for ${Context}: $requiredCapability"
        }
    }
}

function Test-BlockedArtifacts {
    param(
        [string]$RootPath,
        [string]$Context
    )

    $blocked = @(".git", ".github", ".vs", "bin", "obj", "TestResults")

    foreach ($name in $blocked) {
        $found = Get-ChildItem $RootPath -Force -Recurse -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -eq $name } |
            Select-Object -First 1

        if ($found) {
            throw "Blocked development artifact found in $Context output: $($found.FullName)"
        }
    }
}

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$versionFile = Join-Path $RepoRoot "VERSION"
if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = (Get-Content -LiteralPath $versionFile -Raw).Trim()
}
if ([string]::IsNullOrWhiteSpace($Version)) {
    throw "VERSION is empty."
}
$ArtifactsDir = Join-Path $RepoRoot "artifacts"
$PublishDir = Join-Path (Join-Path $ArtifactsDir "publish") $Runtime
$ReleaseDir = Join-Path $ArtifactsDir "release"
$ExePath = Join-Path $PublishDir "csokit.exe"
$NativeDllPath = Join-Path $PublishDir "CsoKit.Native.dll"
$ReleaseNotesPath = Join-Path $PublishDir "RELEASE_NOTES.md"
$ThirdPartyNoticesPath = Join-Path $PublishDir "THIRD_PARTY_NOTICES.md"
$ManifestPath = Join-Path $PublishDir "SHA256SUMS.txt"
$ZipPath = Join-Path $ReleaseDir "csokit-$Version-$Runtime.zip"
$ZipCheckDir = Join-Path $ArtifactsDir "verify-release-check"

Write-Host "CsoKit Release Verifier"
Write-Host "Version: $Version"
Write-Host "Runtime: $Runtime"
Write-Host ""

if ($Runtime -ne "win-x64") {
    throw "Native backend release verification currently supports win-x64 only. Runtime requested: $Runtime"
}

if (-not (Test-Path $PublishDir)) {
    throw "Publish directory was not found: $PublishDir"
}

if (-not (Test-Path $ExePath)) {
    throw "Executable was not found: $ExePath"
}

if (-not (Test-Path $NativeDllPath)) {
    throw "Native backend DLL was not found: $NativeDllPath"
}

if (-not (Test-Path $ReleaseNotesPath)) {
    throw "Release notes were not found: $ReleaseNotesPath"
}

if (-not (Test-Path $ThirdPartyNoticesPath)) {
    throw "Third-party notices were not found: $ThirdPartyNoticesPath"
}

if (-not (Test-Path $ManifestPath)) {
    throw "SHA256 manifest was not found: $ManifestPath"
}

if (-not (Test-Path $ZipPath)) {
    throw "Release ZIP was not found: $ZipPath"
}

Test-BlockedArtifacts -RootPath $PublishDir -Context "publish"

$versionText = ((& $ExePath --version) | Out-String).Trim()

if ($LASTEXITCODE -ne 0) {
    throw "Version command failed."
}

if ($versionText -notmatch [regex]::Escape($Version)) {
    throw "Version mismatch. Output: $versionText"
}

$helpText = ((& $ExePath --help) | Out-String).Trim()

if ($LASTEXITCODE -ne 0) {
    throw "Help command failed."
}

foreach ($required in @("info", "verify", "repair", "analyze", "detect", "decompress", "compress", "codecs", "native-info", "--json", "--quiet", "--profile", "--threads", "--block", "--zopfli", "--codec-report", "game-safe|compat|fast|smallest|archive-smallest")) {
    if ($helpText -notmatch [regex]::Escape($required)) {
        throw "Help output does not contain required text: $required"
    }
}

Invoke-NativeInfoCheck -ExecutablePath $ExePath -Context "publish"

$manifestLines = Get-Content $ManifestPath | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

if ($manifestLines.Count -lt 4) {
    throw "SHA256 manifest looks incomplete."
}

$manifestText = $manifestLines | Out-String

foreach ($requiredFile in @("csokit.exe", "CsoKit.Native.dll", "README.md", "LICENSE.txt", "RELEASE_NOTES.md", "THIRD_PARTY_NOTICES.md")) {
    if ($manifestText -notmatch [regex]::Escape($requiredFile)) {
        throw "SHA256 manifest does not contain required file: $requiredFile"
    }
}

Remove-Item $ZipCheckDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $ZipCheckDir | Out-Null

try {
    Expand-Archive -Path $ZipPath -DestinationPath $ZipCheckDir -Force

    $ZipExePath = Join-Path $ZipCheckDir "csokit.exe"
    $ZipNativeDllPath = Join-Path $ZipCheckDir "CsoKit.Native.dll"
    $ZipManifestPath = Join-Path $ZipCheckDir "SHA256SUMS.txt"
    $ZipReleaseNotesPath = Join-Path $ZipCheckDir "RELEASE_NOTES.md"
    $ZipThirdPartyNoticesPath = Join-Path $ZipCheckDir "THIRD_PARTY_NOTICES.md"

    if (-not (Test-Path $ZipExePath)) {
        throw "Executable was not found inside ZIP: $ZipExePath"
    }

    if (-not (Test-Path $ZipNativeDllPath)) {
        throw "Native backend DLL was not found inside ZIP: $ZipNativeDllPath"
    }

    if (-not (Test-Path $ZipManifestPath)) {
        throw "SHA256 manifest was not found inside ZIP: $ZipManifestPath"
    }

    if (-not (Test-Path $ZipReleaseNotesPath)) {
        throw "Release notes were not found inside ZIP: $ZipReleaseNotesPath"
    }

    if (-not (Test-Path $ZipThirdPartyNoticesPath)) {
        throw "Third-party notices were not found inside ZIP: $ZipThirdPartyNoticesPath"
    }

    Test-BlockedArtifacts -RootPath $ZipCheckDir -Context "ZIP"

    $zipManifestText = Get-Content $ZipManifestPath | Out-String

    foreach ($requiredFile in @("csokit.exe", "CsoKit.Native.dll", "README.md", "LICENSE.txt", "RELEASE_NOTES.md", "THIRD_PARTY_NOTICES.md")) {
        if ($zipManifestText -notmatch [regex]::Escape($requiredFile)) {
            throw "ZIP SHA256 manifest does not contain required file: $requiredFile"
        }
    }

    Invoke-NativeInfoCheck -ExecutablePath $ZipExePath -Context "ZIP"
}
finally {
    Remove-Item $ZipCheckDir -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "[PASS] Release verification completed"
Write-Host "Executable: $ExePath"
Write-Host "NativeDLL:  $NativeDllPath"
Write-Host "ZIP:        $ZipPath"
