[CmdletBinding()]
param(
    [string]$InputIso,

    [string]$Configuration = "Release",

    [string]$Runtime = "win-x64",

    [switch]$SkipRealIsoGates,

    [switch]$KeepArtifacts,

    [switch]$Quiet
)

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "OutputFileNamePolicy.ps1")

function Write-GateHeader {
    param(
        [string]$Title
    )

    Write-Host ""
    Write-Host $Title
    Write-Host ($Title -replace ".", "-")
}

function Invoke-GateStep {
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

function Get-ResolvedIsoPath {
    param(
        [string]$Path
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        throw "InputIso is required unless -SkipRealIsoGates is specified."
    }

    $resolved = Resolve-Path -LiteralPath $Path -ErrorAction SilentlyContinue

    if (-not $resolved) {
        throw "Input ISO was not found: $Path"
    }

    $item = Get-Item -LiteralPath $resolved.Path -ErrorAction Stop

    if ($item.PSIsContainer) {
        throw "InputIso must point to an ISO file, not a directory: $($item.FullName)"
    }

    if (-not [string]::Equals($item.Extension, ".iso", [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "InputIso must use the .iso extension: $($item.FullName)"
    }

    return $item.FullName
}

function Invoke-Exe {
    param(
        [string[]]$Arguments,
        [int[]]$ExpectedExitCodes,
        [string]$StepName
    )

    $output = (& $ExePath @Arguments) 2>&1
    $exitCode = $LASTEXITCODE
    $text = ($output | Out-String).Trim()

    if (-not $Quiet -and -not [string]::IsNullOrWhiteSpace($text)) {
        Write-Host $text
    }

    if ($ExpectedExitCodes -notcontains $exitCode) {
        throw "$StepName expected exit code $($ExpectedExitCodes -join ', '), got $exitCode. Output: $text"
    }

    return $text
}

function Invoke-ExeJson {
    param(
        [string[]]$Arguments,
        [int[]]$ExpectedExitCodes,
        [string]$StepName
    )

    $text = Invoke-Exe -Arguments $Arguments -ExpectedExitCodes $ExpectedExitCodes -StepName $StepName

    if ([string]::IsNullOrWhiteSpace($text)) {
        throw "$StepName did not produce JSON output."
    }

    try {
        return $text | ConvertFrom-Json
    }
    catch {
        throw "$StepName did not produce valid JSON. Output: $text"
    }
}

function Test-HelpSmoke {
    $text = Invoke-Exe -Arguments @( "--help" ) -ExpectedExitCodes @(0) -StepName "published help smoke"

    foreach ($required in @(
        "csokit info <input.cso>",
        "csokit verify <input.cso|input.zso|input.dax>",
        "csokit repair <input.iso|input.cso>",
        "csokit analyze <input.iso>",
        "csokit detect <input>",
        "csokit decompress <input.cso>",
        "csokit compress <input.iso>",
        "--profile <game-safe|compat|fast|smallest|archive-smallest>",
        "[--fast]",
        "--threads <n>",
        "--block <bytes>",
        "--zopfli",
        "--codec-report",
        "codecs",
        "native-info"
    )) {
        if ($text -notmatch [regex]::Escape($required)) {
            throw "Published help output does not contain required text: $required"
        }
    }
}

function Test-VersionSmoke {
    $text = Invoke-Exe -Arguments @( "--version" ) -ExpectedExitCodes @(0) -StepName "published version smoke"

    if ([string]::IsNullOrWhiteSpace($text)) {
        throw "Published --version output was empty."
    }
}

function Test-NativeInfoSmoke {
    $text = Invoke-Exe -Arguments @( "native-info" ) -ExpectedExitCodes @(0) -StepName "published native-info smoke"

    foreach ($required in @(
        "Backend:",
        "Native available:",
        "Native zlib: available",
        "Native libdeflate: available",
        "Native Zopfli: available",
        "LZ4 decode: available"
    )) {
        if ($text -notmatch [regex]::Escape($required)) {
            throw "Published native-info output does not contain required text: $required"
        }
    }
}

function Test-JsonArgumentSmoke {
    $json = Invoke-ExeJson -Arguments @(
        "compress",
        "missing.iso",
        "--measure",
        "--profile",
        "bad",
        "--json"
    ) -ExpectedExitCodes @(2) -StepName "published JSON argument smoke"

    if ($json.schemaVersion -ne 1) {
        throw "Published JSON argument smoke did not return schemaVersion = 1."
    }

    if ($json.success -ne $false) {
        throw "Published JSON argument smoke did not report success = false."
    }

    if ($json.error.code -ne "InvalidArguments") {
        throw "Published JSON argument smoke returned unexpected error code: $($json.error.code)"
    }

    if ($json.error.message -notmatch "Supported profiles: game-safe\|compat\|fast\|smallest\|archive-smallest") {
        throw "Published JSON argument smoke returned an unexpected message: $($json.error.message)"
    }
}

function Test-MeasureProfile {
    param(
        [string]$Profile
    )

    $json = Invoke-ExeJson -Arguments @(
        "compress",
        $ResolvedInputIso,
        "--measure",
        "--profile",
        $Profile,
        "--json"
    ) -ExpectedExitCodes @(0) -StepName "published measure smoke ($Profile)"

    if ($json.schemaVersion -ne 1) {
        throw "Measure JSON for $Profile did not return schemaVersion = 1."
    }

    if ($json.command -ne "compress" -or $json.mode -ne "measure" -or $json.success -ne $true) {
        throw "Measure JSON for $Profile returned an unexpected command/mode/success combination."
    }

    if ($json.options.profile.name -ne $Profile) {
        throw "Measure JSON for $Profile reported profile '$($json.options.profile.name)'."
    }

    if ([UInt64]$json.metrics.originalBytes -ne [UInt64]$OriginalLength) {
        throw "Measure JSON for $Profile reported originalBytes $($json.metrics.originalBytes), expected $OriginalLength."
    }

    if ([UInt64]$json.metrics.estimatedBytes -le 0) {
        throw "Measure JSON for $Profile reported a non-positive estimatedBytes value."
    }
}

function Test-PublishedRoundtripProfile {
    param(
        [string]$Profile
    )

    $profileRoot = Join-Path $WorkDir $Profile
    $csoPath = Join-Path $profileRoot "out.cso"
    $restoredIsoPath = Join-Path $profileRoot "back.iso"
    Assert-CsoKitOutputFileName -Path $csoPath -Context "Published profile CSO"
    Assert-CsoKitOutputFileName -Path $restoredIsoPath -Context "Published profile restored ISO"

    Remove-Item -LiteralPath $profileRoot -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force -Path $profileRoot | Out-Null

    $compressJson = Invoke-ExeJson -Arguments @(
        "compress",
        $ResolvedInputIso,
        "-o",
        $csoPath,
        "--profile",
        $Profile,
        "--force",
        "--json"
    ) -ExpectedExitCodes @(0) -StepName "published compress smoke ($Profile)"

    if ($compressJson.schemaVersion -ne 1 -or $compressJson.command -ne "compress" -or $compressJson.mode -ne "write" -or $compressJson.success -ne $true) {
        throw "Compress JSON for $Profile returned an unexpected contract shape."
    }

    if ($compressJson.options.profile.name -ne $Profile) {
        throw "Compress JSON for $Profile reported profile '$($compressJson.options.profile.name)'."
    }

    if (-not (Test-Path -LiteralPath $csoPath)) {
        throw "Compressed CSO was not produced for ${Profile}: $csoPath"
    }

    $csoLength = (Get-Item -LiteralPath $csoPath).Length

    if ($csoLength -le 0) {
        throw "Compressed CSO length was not positive for $Profile."
    }

    $verifyJson = Invoke-ExeJson -Arguments @(
        "verify",
        $csoPath,
        "--deep",
        "--sha256",
        "--json"
    ) -ExpectedExitCodes @(0) -StepName "published verify smoke ($Profile)"

    if ($verifyJson.command -ne "verify" -or $verifyJson.success -ne $true) {
        throw "Verify JSON for $Profile did not report success."
    }

    $decompressJson = Invoke-ExeJson -Arguments @(
        "decompress",
        $csoPath,
        "-o",
        $restoredIsoPath,
        "--force",
        "--json"
    ) -ExpectedExitCodes @(0) -StepName "published decompress smoke ($Profile)"

    if ($decompressJson.command -ne "decompress" -or $decompressJson.success -ne $true) {
        throw "Decompress JSON for $Profile did not report success."
    }

    if (-not (Test-Path -LiteralPath $restoredIsoPath)) {
        throw "Restored ISO was not produced for ${Profile}: $restoredIsoPath"
    }

    $restoredLength = (Get-Item -LiteralPath $restoredIsoPath).Length

    if ($restoredLength -ne $OriginalLength) {
        throw "Restored ISO length mismatch for $Profile. Expected $OriginalLength, got $restoredLength."
    }

    $restoredHash = (Get-FileHash -LiteralPath $restoredIsoPath -Algorithm SHA256).Hash

    if (-not [string]::Equals($restoredHash, $OriginalHash, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Restored ISO SHA256 mismatch for $Profile. Expected $OriginalHash, got $restoredHash."
    }

    [PSCustomObject]@{
        Profile = $Profile
        CsoBytes = $csoLength
        Ratio = if ($OriginalLength -eq 0) { 0 } else { [double]$csoLength / [double]$OriginalLength }
        IsoBytes = $restoredLength
        Status = "PASS"
    }
}

$RepoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
$SolutionPath = Join-Path $RepoRoot "CsoKit.slnx"
$CliProject = Join-Path $RepoRoot "src\CsoKit.Cli\CsoKit.Cli.csproj"
$ArtifactsRoot = Join-Path $RepoRoot "artifacts"
$PublishDir = Join-Path (Join-Path $ArtifactsRoot "published-exe-smoke") $Runtime
$WorkDir = Join-Path (Join-Path $ArtifactsRoot "published-exe-smoke-work") $Runtime
$ExePath = Join-Path $PublishDir "csokit.exe"
$NativeDllPath = Join-Path $RepoRoot "artifacts\native-build\win-x64\Release\CsoKit.Native.dll"
$PublishNativeDllPath = Join-Path $PublishDir "CsoKit.Native.dll"

if (-not (Test-Path -LiteralPath $SolutionPath)) {
    throw "Solution file was not found: $SolutionPath"
}

if (-not (Test-Path -LiteralPath $CliProject)) {
    throw "CLI project was not found: $CliProject"
}

if ($Runtime -ne "win-x64") {
    throw "Published native smoke currently supports win-x64 only. Runtime requested: $Runtime"
}

$ResolvedInputIso = $null
$OriginalLength = 0
$OriginalHash = $null

if (-not $SkipRealIsoGates) {
    $ResolvedInputIso = Get-ResolvedIsoPath -Path $InputIso
    $OriginalLength = (Get-Item -LiteralPath $ResolvedInputIso).Length
    $OriginalHash = (Get-FileHash -LiteralPath $ResolvedInputIso -Algorithm SHA256).Hash
}

Write-GateHeader -Title "CsoKit Published EXE Smoke"
Write-Host "Repo:          $RepoRoot"
Write-Host "Configuration: $Configuration"
Write-Host "Runtime:       $Runtime"
Write-Host "PublishDir:    $PublishDir"
Write-Host "Real ISO:      $(if ($SkipRealIsoGates) { 'skipped' } else { $ResolvedInputIso })"
Write-Host "Artifacts:     $(if ($KeepArtifacts) { 'kept' } else { 'deleted on success' })"

Invoke-GateStep -Name "dotnet restore" -Action {
    Invoke-DotNet -StepName "dotnet restore" -Arguments @(
        "restore",
        $SolutionPath,
        "-r",
        $Runtime,
        "-p:NuGetAudit=false"
    )
}

Invoke-GateStep -Name "build native backend" -Action {
    & (Join-Path $PSScriptRoot "Build-Native.ps1") -Configuration Release -Platform x64

    if (-not (Test-Path -LiteralPath $NativeDllPath -PathType Leaf)) {
        throw "Native DLL was not produced: $NativeDllPath"
    }
}

Invoke-GateStep -Name "dotnet publish" -Action {
    Remove-Item -LiteralPath $PublishDir -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force -Path $PublishDir | Out-Null

    Invoke-DotNet -StepName "dotnet publish" -Arguments @(
        "publish",
        $CliProject,
        "-c",
        $Configuration,
        "-r",
        $Runtime,
        "--no-restore",
        "--self-contained",
        "false",
        "-o",
        $PublishDir,
        "-p:PublishSingleFile=false",
        "-p:DebugType=None",
        "-p:DebugSymbols=false",
        "-p:NuGetAudit=false"
    )

    if (-not (Test-Path -LiteralPath $ExePath -PathType Leaf)) {
        throw "Published executable was not found: $ExePath"
    }

    Copy-Item -LiteralPath $NativeDllPath -Destination $PublishNativeDllPath -Force

    if (-not (Test-Path -LiteralPath $PublishNativeDllPath -PathType Leaf)) {
        throw "Published native DLL was not staged: $PublishNativeDllPath"
    }
}

Invoke-GateStep -Name "published help smoke" -Action {
    Test-HelpSmoke
}

Invoke-GateStep -Name "published version smoke" -Action {
    Test-VersionSmoke
}

Invoke-GateStep -Name "published native-info smoke" -Action {
    Test-NativeInfoSmoke
}

Invoke-GateStep -Name "published JSON argument smoke" -Action {
    Test-JsonArgumentSmoke
}

if (-not $SkipRealIsoGates) {
    Invoke-GateStep -Name "published measure smoke" -Action {
        Test-MeasureProfile -Profile "smallest"
        Test-MeasureProfile -Profile "fast"
    }

    Invoke-GateStep -Name "published profile roundtrip smoke" -Action {
        Remove-Item -LiteralPath $WorkDir -Recurse -Force -ErrorAction SilentlyContinue
        New-Item -ItemType Directory -Force -Path $WorkDir | Out-Null

        $results = @()
        foreach ($profile in @( "game-safe", "compat", "fast", "smallest" )) {
            $results += Test-PublishedRoundtripProfile -Profile $profile
        }

        Write-Host ""
        Write-Host "Published EXE profile roundtrip result"
        Write-Host "--------------------------------------"
        $results |
            Select-Object Profile, CsoBytes, @{ Name = "Ratio"; Expression = { "{0:P2}" -f $_.Ratio } }, IsoBytes, Status |
            Format-Table -AutoSize

        if (-not $KeepArtifacts) {
            Remove-Item -LiteralPath $WorkDir -Recurse -Force -ErrorAction SilentlyContinue
            Write-Host "Artifacts removed."
        }
        else {
            Write-Host "Artifacts kept: $WorkDir"
        }
    }
}
else {
    Write-Host ""
    Write-Host "[published real ISO smoke] SKIPPED"
}

Write-Host ""
Write-Host "Published EXE smoke result"
Write-Host "--------------------------"
Write-Host "Status: PASS"
Write-Host "Executable: $ExePath"

$global:LASTEXITCODE = 0
