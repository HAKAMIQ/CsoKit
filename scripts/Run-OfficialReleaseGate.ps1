[CmdletBinding()]
param(
    [string]$Version = "",
    [string]$Runtime = "win-x64",
    [string]$InputIso,
    [switch]$SkipRealIsoGate,
    [switch]$SkipNuGetAudit,
    [switch]$AllowDirty,
    [switch]$KeepArtifacts
)

$ErrorActionPreference = "Stop"

function Invoke-Step {
    param(
        [string]$Name,
        [scriptblock]$Action
    )

    Write-Host ""
    Write-Host "[$Name]"
    $started = Get-Date

    try {
        & $Action
    }
    catch {
        Write-Host "[$Name] FAILED"
        throw
    }

    $elapsed = (Get-Date) - $started
    Write-Host ("[$Name] PASS ({0:N1}s)" -f $elapsed.TotalSeconds)
}

function Invoke-DotNet {
    param(
        [string[]]$Arguments,
        [string]$StepName
    )

    & dotnet @Arguments

    if ($LASTEXITCODE -ne 0) {
        throw "$StepName failed with exit code $LASTEXITCODE."
    }
}

function Get-RestoreAuditArgs {
    if ($SkipNuGetAudit) {
        return @("-p:NuGetAudit=false")
    }

    return @()
}

function Get-RelativePathCompat {
    param(
        [string]$BasePath,
        [string]$FullPath
    )

    $baseFullPath = [System.IO.Path]::GetFullPath($BasePath).TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
    $itemFullPath = [System.IO.Path]::GetFullPath($FullPath)
    $baseUri = [System.Uri]::new($baseFullPath)
    $itemUri = [System.Uri]::new($itemFullPath)
    $relativeUri = $baseUri.MakeRelativeUri($itemUri)
    $relativePath = [System.Uri]::UnescapeDataString($relativeUri.ToString())
    return $relativePath.Replace('\', '/')
}

function Assert-ProjectVersion {
    param(
        [string]$ProjectPath,
        [string]$ExpectedVersion,
        [string]$Name
    )

    if (-not (Test-Path -LiteralPath $ProjectPath)) {
        throw "$Name project file was not found: $ProjectPath"
    }

    [xml]$projectXml = Get-Content -LiteralPath $ProjectPath
    $propertyGroups = @($projectXml.Project.PropertyGroup)
    $version = ($propertyGroups | ForEach-Object { $_.Version } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -First 1)
    $packageVersion = ($propertyGroups | ForEach-Object { $_.PackageVersion } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -First 1)
    $informationalVersion = ($propertyGroups | ForEach-Object { $_.InformationalVersion } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -First 1)

    foreach ($field in @(@("Version", $version), @("PackageVersion", $packageVersion), @("InformationalVersion", $informationalVersion))) {
        if ($field[1] -ne $ExpectedVersion) {
            throw "$Name $($field[0]) mismatch. Expected $ExpectedVersion, found $($field[1])."
        }
    }
}

function Assert-NativeVersion {
    param(
        [string]$NativeSourcePath,
        [string]$CMakePath,
        [string]$ExpectedVersion
    )

    $parts = $ExpectedVersion.Split('-')[0].Split('.')

    if ($parts.Count -ne 3) {
        throw "Expected a semantic version with three numeric components: $ExpectedVersion"
    }

    $sourceText = Get-Content -LiteralPath $NativeSourcePath -Raw
    $cmakeText = Get-Content -LiteralPath $CMakePath -Raw

    if ($cmakeText -notmatch [regex]::Escape("VERSION $($parts[0]).$($parts[1]).$($parts[2])")) {
        throw "Native CMake version does not match $ExpectedVersion."
    }

    foreach ($pair in @(
        @("major", $parts[0]),
        @("minor", $parts[1]),
        @("patch", $parts[2])
    )) {
        if ($sourceText -notmatch "version->$($pair[0])\s*=\s*$($pair[1]);") {
            throw "Native source version->$($pair[0]) does not match $ExpectedVersion."
        }
    }
}

function Assert-GitClean {
    param([string]$RepoRoot)

    $git = Get-Command git -ErrorAction SilentlyContinue
    if (-not $git) {
        Write-Host "git was not found; skipping git clean check."
        return
    }

    $status = @(git -C $RepoRoot status --porcelain)
    if ($LASTEXITCODE -ne 0) {
        throw "git status failed."
    }

    if ($status.Count -ne 0) {
        $status | ForEach-Object { Write-Host $_ }
        throw "Working tree must be clean for official release. Commit changes or pass -AllowDirty for local validation only."
    }
}

function Assert-NoTrackedForbiddenArtifacts {
    param([string]$RepoRoot)

    $git = Get-Command git -ErrorAction SilentlyContinue
    if (-not $git) {
        Write-Host "git was not found; skipping tracked artifact check."
        return
    }

    $tracked = @(git -C $RepoRoot ls-files)
    if ($LASTEXITCODE -ne 0) {
        throw "git ls-files failed."
    }

    $pattern = '(^|/)(artifacts|bin|obj|TestResults|\.vs)(/|$)|\.(tmp|partial)$'
    $bad = @($tracked | Where-Object { $_ -match $pattern })

    if ($bad.Count -ne 0) {
        $bad | ForEach-Object { Write-Host $_ }
        throw "Tracked forbidden release artifacts were found."
    }
}

function Test-BlockedArtifacts {
    param(
        [string]$RootPath,
        [string]$Context
    )

    if (-not (Test-Path -LiteralPath $RootPath)) {
        throw "$Context directory was not found: $RootPath"
    }

    $blockedNames = @(".git", ".github", ".vs", "bin", "obj", "TestResults", "artifacts")
    $found = Get-ChildItem -LiteralPath $RootPath -Force -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $blockedNames -contains $_.Name -or $_.Name.EndsWith(".tmp", [System.StringComparison]::OrdinalIgnoreCase) -or $_.Name.EndsWith(".partial", [System.StringComparison]::OrdinalIgnoreCase) } |
        Select-Object -First 1

    if ($found) {
        throw "Blocked development artifact found in $Context output: $($found.FullName)"
    }
}

function Copy-ReleaseDocuments {
    param([string]$Destination)

    foreach ($file in @("README.md", "LICENSE.txt", "RELEASE_NOTES.md", "THIRD_PARTY_NOTICES.md")) {
        $path = Join-Path $RepoRoot $file
        if (Test-Path -LiteralPath $path) {
            Copy-Item -LiteralPath $path -Destination (Join-Path $Destination $file) -Force
        }
    }
}

function New-Sha256Manifest {
    param([string]$Directory)

    $manifest = Join-Path $Directory "SHA256SUMS.txt"

    Get-ChildItem -LiteralPath $Directory -File -Recurse |
        Where-Object { $_.Name -ne "SHA256SUMS.txt" } |
        Sort-Object FullName |
        ForEach-Object {
            $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
            $relative = Get-RelativePathCompat -BasePath $Directory -FullPath $_.FullName
            "$hash  $relative"
        } |
        Set-Content -LiteralPath $manifest -Encoding UTF8
}

function New-ReleaseZip {
    param(
        [string]$SourceDirectory,
        [string]$ZipPath
    )

    if (Test-Path -LiteralPath $ZipPath) {
        Remove-Item -LiteralPath $ZipPath -Force
    }

    Compress-Archive -Path (Join-Path $SourceDirectory "*") -DestinationPath $ZipPath -Force
    $hash = (Get-FileHash -LiteralPath $ZipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    Set-Content -LiteralPath "$ZipPath.sha256.txt" -Value "$hash  $(Split-Path -Leaf $ZipPath)" -Encoding UTF8
}

function Invoke-CliSmoke {
    param([string]$ExePath)

    if (-not (Test-Path -LiteralPath $ExePath)) {
        throw "CLI executable was not found: $ExePath"
    }

    $versionText = ((& $ExePath --version) | Out-String).Trim()
    if ($LASTEXITCODE -ne 0) {
        throw "CLI --version smoke failed."
    }

    if ($versionText -notmatch [regex]::Escape($Version)) {
        throw "CLI --version does not contain $Version. Output: $versionText"
    }

    $helpText = ((& $ExePath --help) | Out-String).Trim()
    if ($LASTEXITCODE -ne 0) {
        throw "CLI --help smoke failed."
    }

    foreach ($required in @("verify", "compress", "decompress", "repair", "analyze", "detect", "codecs", "native-info", "--json", "--profile", "--threads", "--codec-report")) {
        if ($helpText -notmatch [regex]::Escape($required)) {
            throw "CLI help output does not contain required text: $required"
        }
    }
}

function Invoke-RawIsoDeepVerifySmoke {
    param(
        [string]$ExePath,
        [string]$IsoPath
    )

    if ([string]::IsNullOrWhiteSpace($IsoPath)) {
        throw "InputIso was not provided. This function is for optional real-corpus smoke only."
    }

    $resolved = Resolve-Path -LiteralPath $IsoPath -ErrorAction Stop
    $jsonText = ((& $ExePath verify $resolved.Path --deep --json) | Out-String).Trim()

    if ($LASTEXITCODE -ne 0) {
        Write-Host $jsonText
        throw "Raw ISO deep verify smoke failed with exit code $LASTEXITCODE."
    }

    $json = $jsonText | ConvertFrom-Json

    if ($json.success -ne $true) {
        Write-Host $jsonText
        throw "Raw ISO deep verify smoke did not report success=true."
    }

    if ($json.format -ne "RawIso") {
        Write-Host $jsonText
        throw "Raw ISO deep verify smoke returned unexpected format: $($json.format)"
    }
}

function Test-ShouldRunRealIsoGate {
    if ($SkipRealIsoGate) {
        return $false
    }

    return -not [string]::IsNullOrWhiteSpace($InputIso)
}

if ($Runtime -ne "win-x64") {
    throw "Only win-x64 is supported by the official release gate. Runtime requested: $Runtime"
}

$RepoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
$versionFile = Join-Path $RepoRoot "VERSION"
if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = (Get-Content -LiteralPath $versionFile -Raw).Trim()
}
if ([string]::IsNullOrWhiteSpace($Version)) {
    throw "VERSION is empty."
}
$SolutionPath = Join-Path $RepoRoot "CsoKit.slnx"
$CoreProject = Join-Path $RepoRoot "src\CsoKit.Core\CsoKit.Core.csproj"
$CliProject = Join-Path $RepoRoot "src\CsoKit.Cli\CsoKit.Cli.csproj"
$AppProject = Join-Path $RepoRoot "src\CsoKit.App\CsoKit.App.csproj"
$NativeSource = Join-Path $RepoRoot "native\CsoKit.Native\src\csokit_native.cpp"
$NativeCMake = Join-Path $RepoRoot "native\CsoKit.Native\CMakeLists.txt"
$ArtifactsDir = Join-Path $RepoRoot "artifacts"
$PublishRoot = Join-Path $ArtifactsDir "publish"
$ReleaseRoot = Join-Path $ArtifactsDir "release"
$CliPublishDir = Join-Path $PublishRoot "CsoKit-cli-$Runtime"
$AppPublishDir = Join-Path $PublishRoot "CsoKit-app-$Runtime"
$CliZip = Join-Path $ReleaseRoot "csokit-$Version-cli-$Runtime.zip"
$AppZip = Join-Path $ReleaseRoot "csokit-$Version-app-$Runtime.zip"

Write-Host "CsoKit Official Release Gate"
Write-Host "-------------------------------------"
Write-Host "Repo:       $RepoRoot"
Write-Host "Version:    $Version"
Write-Host "Runtime:    $Runtime"
Write-Host "NuGetAudit: $(if ($SkipNuGetAudit) { 'disabled by request' } else { 'enabled' })"
Write-Host "Real ISO:   $(if (Test-ShouldRunRealIsoGate) { $InputIso } else { 'not provided; optional real-corpus smoke skipped' })"
Write-Host ""

Assert-ProjectVersion -ProjectPath $CoreProject -ExpectedVersion $Version -Name "Core"
Assert-ProjectVersion -ProjectPath $CliProject -ExpectedVersion $Version -Name "CLI"
Assert-ProjectVersion -ProjectPath $AppProject -ExpectedVersion $Version -Name "App"
Assert-NativeVersion -NativeSourcePath $NativeSource -CMakePath $NativeCMake -ExpectedVersion $Version

if (-not $AllowDirty) {
    Assert-GitClean -RepoRoot $RepoRoot
}

Assert-NoTrackedForbiddenArtifacts -RepoRoot $RepoRoot

if (-not $KeepArtifacts) {
    Remove-Item -LiteralPath $ArtifactsDir -Recurse -Force -ErrorAction SilentlyContinue
}

New-Item -ItemType Directory -Force -Path @($PublishRoot, $ReleaseRoot) | Out-Null

$restoreAuditArgs = Get-RestoreAuditArgs

Invoke-Step "dotnet clean" {
    Invoke-DotNet -StepName "dotnet clean" -Arguments @("clean", $SolutionPath)
}

Invoke-Step "dotnet restore" {
    Invoke-DotNet -StepName "dotnet restore" -Arguments (@("restore", $SolutionPath) + $restoreAuditArgs)
}

Invoke-Step "dotnet build Release" {
    Invoke-DotNet -StepName "dotnet build Release" -Arguments (@("build", $SolutionPath, "-c", "Release", "--no-restore") + $restoreAuditArgs)
}

Invoke-Step "dotnet test Release" {
    Invoke-DotNet -StepName "dotnet test Release" -Arguments @("test", $SolutionPath, "-c", "Release", "--no-build")
}

Invoke-Step "publish CLI" {
    Invoke-DotNet -StepName "publish CLI" -Arguments (@(
        "publish", $CliProject,
        "-c", "Release",
        "-r", $Runtime,
        "--self-contained", "false",
        "--no-restore",
        "-o", $CliPublishDir
    ) + $restoreAuditArgs)

    Copy-ReleaseDocuments -Destination $CliPublishDir
    New-Sha256Manifest -Directory $CliPublishDir
    Test-BlockedArtifacts -RootPath $CliPublishDir -Context "CLI publish"
}

Invoke-Step "publish App" {
    Invoke-DotNet -StepName "publish App" -Arguments (@(
        "publish", $AppProject,
        "-c", "Release",
        "-r", $Runtime,
        "--self-contained", "false",
        "--no-restore",
        "-o", $AppPublishDir
    ) + $restoreAuditArgs)

    Copy-ReleaseDocuments -Destination $AppPublishDir
    New-Sha256Manifest -Directory $AppPublishDir
    Test-BlockedArtifacts -RootPath $AppPublishDir -Context "App publish"
}

Invoke-Step "CLI smoke" {
    Invoke-CliSmoke -ExePath (Join-Path $CliPublishDir "csokit.exe")
}

if (Test-ShouldRunRealIsoGate) {
    Invoke-Step "Raw ISO deep verify smoke" {
        Invoke-RawIsoDeepVerifySmoke -ExePath (Join-Path $CliPublishDir "csokit.exe") -IsoPath $InputIso
    }
}
else {
    Write-Host ""
    Write-Host "[Raw ISO deep verify smoke] SKIPPED"
    Write-Host "Reason: no -InputIso was provided, or -SkipRealIsoGate was used."
    Write-Host "Coverage: Raw ISO deep verification is still covered by unit/CLI contract tests in dotnet test."
}

Invoke-Step "create release ZIPs" {
    New-ReleaseZip -SourceDirectory $CliPublishDir -ZipPath $CliZip
    New-ReleaseZip -SourceDirectory $AppPublishDir -ZipPath $AppZip
}

$summaryPath = Join-Path $ReleaseRoot "RELEASE_GATE_SUMMARY.txt"
@(
    "CsoKit Official Release Gate",
    "Version: $Version",
    "Runtime: $Runtime",
    "Timestamp: $((Get-Date).ToString('o'))",
    "Result: PASS",
    "CLI ZIP: $CliZip",
    "App ZIP: $AppZip",
    "NuGetAudit: $(if ($SkipNuGetAudit) { 'disabled by request' } else { 'enabled' })",
    "Raw ISO real-corpus gate: $(if (Test-ShouldRunRealIsoGate) { 'passed' } else { 'skipped' })"
) | Set-Content -LiteralPath $summaryPath -Encoding UTF8

Write-Host ""
Write-Host "Official release gate result"
Write-Host "----------------------------"
Write-Host "Status: PASS"
Write-Host "CLI ZIP: $CliZip"
Write-Host "App ZIP: $AppZip"
Write-Host "Summary: $summaryPath"
