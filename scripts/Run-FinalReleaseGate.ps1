[CmdletBinding()]
param(
    [string]$Version = "",
    [string]$Runtime = "win-x64",
    [string]$InputIso,
    [switch]$SkipRealIsoGates,
    [switch]$SkipPublishedExeSmoke,
    [switch]$SkipReleasePackage,
    [switch]$KeepArtifacts,
    [switch]$AllowDirty
)

$ErrorActionPreference = "Stop"

function Invoke-Step {
    param(
        [string]$Name,
        [scriptblock]$Command
    )

    Write-Host ""
    Write-Host "[$Name]"

    $start = Get-Date
    & $Command

    if ($LASTEXITCODE -ne 0) {
        throw "$Name failed with exit code $LASTEXITCODE."
    }

    $elapsed = (Get-Date) - $start
    Write-Host "[$Name] PASS ($([math]::Round($elapsed.TotalSeconds, 1))s)"
}

function Invoke-PowerShellScriptStep {
    param(
        [string]$Name,
        [string]$ScriptPath,
        [hashtable]$Arguments
    )

    Invoke-Step $Name {
        & $ScriptPath @Arguments
    }
}

function Assert-ProjectVersion {
    param(
        [string]$ProjectPath,
        [string]$ExpectedVersion,
        [string]$Name
    )

    if (-not (Test-Path $ProjectPath)) {
        throw "$Name project file was not found: $ProjectPath"
    }

    [xml]$projectXml = Get-Content $ProjectPath

    $version = $projectXml.Project.PropertyGroup.Version | Select-Object -First 1
    $packageVersion = $projectXml.Project.PropertyGroup.PackageVersion | Select-Object -First 1
    $informationalVersion = $projectXml.Project.PropertyGroup.InformationalVersion | Select-Object -First 1

    foreach ($value in @($version, $packageVersion, $informationalVersion)) {
        if ([string]::IsNullOrWhiteSpace($value)) {
            throw "$Name project has an empty version field."
        }

        if ($value -ne $ExpectedVersion) {
            throw "$Name project version mismatch. Expected $ExpectedVersion, found $value."
        }
    }
}

function Assert-GitClean {
    param(
        [string]$RepoRoot
    )

    $status = @(git -C $RepoRoot status --porcelain)

    if ($LASTEXITCODE -ne 0) {
        throw "git status failed."
    }

    if ($status.Count -ne 0) {
        Write-Host "Git working tree is not clean:"
        $status | ForEach-Object { Write-Host $_ }
        throw "Working tree must be clean for the final release gate. Commit changes first or pass -AllowDirty only while validating an uncommitted gate script."
    }
}

function Assert-NoTrackedArtifacts {
    param(
        [string]$RepoRoot
    )

    $trackedArtifacts = @(git -C $RepoRoot ls-files artifacts)

    if ($LASTEXITCODE -ne 0) {
        throw "git ls-files artifacts failed."
    }

    if ($trackedArtifacts.Count -ne 0) {
        $trackedArtifacts | ForEach-Object { Write-Host $_ }
        throw "Release artifacts are tracked by Git. artifacts/ must stay out of source control."
    }
}

function Assert-FinalNativeBackend {
    param(
        [string]$PublishDir,
        [string]$ExpectedVersion
    )

    $exePath = Join-Path $PublishDir "csokit.exe"
    $nativeDllPath = Join-Path $PublishDir "CsoKit.Native.dll"

    if (-not (Test-Path $exePath)) {
        throw "Published executable was not found for native backend check: $exePath"
    }

    if (-not (Test-Path $nativeDllPath)) {
        throw "Native DLL was not found in final publish directory: $nativeDllPath"
    }

    $nativeInfoOutput = ((& $exePath native-info) | Out-String).Trim()
    if ($LASTEXITCODE -ne 0) {
        Write-Host $nativeInfoOutput
        throw "Final native-info check failed with exit code $LASTEXITCODE."
    }

    if ($nativeInfoOutput -notmatch "Backend:\s+native") {
        Write-Host $nativeInfoOutput
        throw "Final publish did not report native backend."
    }

    if ($nativeInfoOutput -notmatch "Native available:\s+True") {
        Write-Host $nativeInfoOutput
        throw "Final publish did not report native availability."
    }

    foreach ($requiredCapability in @(
        "Native zlib:\s+available",
        "Native libdeflate:\s+available",
        "Native Zopfli:\s+available",
        "LZ4 decode:\s+available"
    )) {
        if ($nativeInfoOutput -notmatch $requiredCapability) {
            Write-Host $nativeInfoOutput
            throw "Final publish did not report required codec capability: $requiredCapability"
        }
    }

    $expectedNativeVersion = [regex]::Escape("Native version: $ExpectedVersion ABI 2")
    if ($nativeInfoOutput -notmatch $expectedNativeVersion) {
        Write-Host $nativeInfoOutput
        throw "Final native version mismatch. Expected: Native version: $ExpectedVersion ABI 2"
    }

    Write-Host $nativeInfoOutput
}

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$versionFile = Join-Path $RepoRoot "VERSION"
if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = (Get-Content -LiteralPath $versionFile -Raw).Trim()
}
if ([string]::IsNullOrWhiteSpace($Version)) {
    throw "VERSION is empty."
}
$ReleaseGateScript = Join-Path $PSScriptRoot "Run-ReleaseGate.ps1"
$PublishedSmokeScript = Join-Path $PSScriptRoot "Run-PublishedExeSmoke.ps1"
$PublishReleaseScript = Join-Path $PSScriptRoot "Publish-Release.ps1"
$VerifyReleaseScript = Join-Path $PSScriptRoot "Verify-Release.ps1"
$PublishSourceScript = Join-Path $PSScriptRoot "Publish-SourcePackage.ps1"
$CliProject = Join-Path $RepoRoot "src\CsoKit.Cli\CsoKit.Cli.csproj"
$CoreProject = Join-Path $RepoRoot "src\CsoKit.Core\CsoKit.Core.csproj"
$AppProject = Join-Path $RepoRoot "src\CsoKit.App\CsoKit.App.csproj"
$PublishDir = Join-Path (Join-Path $RepoRoot "artifacts\publish") $Runtime
$ReleaseZip = Join-Path (Join-Path $RepoRoot "artifacts\release") "csokit-$Version-$Runtime.zip"
$SourceZip = Join-Path (Join-Path $RepoRoot "artifacts\source") "csokit-$Version-source.zip"

Write-Host "CsoKit Final Release Gate"
Write-Host "---------------------------------"
Write-Host "Repo:       $RepoRoot"
Write-Host "Version:    $Version"
Write-Host "Runtime:    $Runtime"
if ($SkipRealIsoGates) {
    Write-Host "Real ISO:   skipped"
}
else {
    Write-Host "Real ISO:   $InputIso"
}
if ($AllowDirty) {
    Write-Host "Git clean:  allow dirty"
}
else {
    Write-Host "Git clean:  required"
}
Write-Host ""

if ($Runtime -ne "win-x64") {
    throw "Only win-x64 is supported by the final release gate. Runtime requested: $Runtime"
}

if (-not $AllowDirty) {
    Assert-GitClean -RepoRoot $RepoRoot
}

Assert-NoTrackedArtifacts -RepoRoot $RepoRoot
Assert-ProjectVersion -ProjectPath $CliProject -ExpectedVersion $Version -Name "CLI"
Assert-ProjectVersion -ProjectPath $CoreProject -ExpectedVersion $Version -Name "Core"
Assert-ProjectVersion -ProjectPath $AppProject -ExpectedVersion $Version -Name "App"

if (-not $SkipRealIsoGates) {
    if ([string]::IsNullOrWhiteSpace($InputIso)) {
        throw "InputIso is required unless -SkipRealIsoGates is supplied."
    }

    if (-not (Test-Path $InputIso)) {
        throw "InputIso was not found: $InputIso"
    }
}

$realGateArguments = @{}
if ($SkipRealIsoGates) {
    $realGateArguments["SkipRealIsoGates"] = $true
}
else {
    $realGateArguments["InputIso"] = $InputIso
}
if ($KeepArtifacts) {
    $realGateArguments["KeepArtifacts"] = $true
}

Invoke-PowerShellScriptStep `
    -Name "consolidated release gate" `
    -ScriptPath $ReleaseGateScript `
    -Arguments $realGateArguments

if (-not $SkipPublishedExeSmoke) {
    Invoke-PowerShellScriptStep `
        -Name "published EXE smoke" `
        -ScriptPath $PublishedSmokeScript `
        -Arguments $realGateArguments
}
else {
    Write-Host ""
    Write-Host "[published EXE smoke] SKIPPED"
}

if (-not $SkipReleasePackage) {
    Invoke-PowerShellScriptStep `
        -Name "publish release package" `
        -ScriptPath $PublishReleaseScript `
        -Arguments @{
            Version = $Version
            Runtime = $Runtime
        }

    Invoke-PowerShellScriptStep `
        -Name "verify release package" `
        -ScriptPath $VerifyReleaseScript `
        -Arguments @{
            Version = $Version
            Runtime = $Runtime
        }

    Invoke-Step "final native backend check" {
        Assert-FinalNativeBackend -PublishDir $PublishDir -ExpectedVersion $Version
    }

    Invoke-PowerShellScriptStep `
        -Name "publish source package" `
        -ScriptPath $PublishSourceScript `
        -Arguments @{
            Version = $Version
        }

    if (-not (Test-Path $ReleaseZip)) {
        throw "Release ZIP was not produced: $ReleaseZip"
    }

    if (-not (Test-Path $SourceZip)) {
        throw "Source ZIP was not produced: $SourceZip"
    }
}
else {
    Write-Host ""
    Write-Host "[release package] SKIPPED"
}

Assert-NoTrackedArtifacts -RepoRoot $RepoRoot

Write-Host ""
Write-Host "Final release gate result"
Write-Host "-------------------------"
Write-Host "Status: PASS"

if (-not $SkipReleasePackage) {
    Write-Host "Release ZIP: $ReleaseZip"
    Write-Host "Source ZIP:  $SourceZip"
}
