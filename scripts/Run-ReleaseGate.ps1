[CmdletBinding()]
param(
    [string]$InputIso,

    [string]$Configuration = "Debug",

    [switch]$SkipRealIsoGates,

    [switch]$KeepArtifacts,

    [switch]$Quiet
)

$ErrorActionPreference = "Stop"

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

function Test-ForbiddenTerms {
    $scanPaths = New-Object System.Collections.Generic.List[string]

    $sourceRoot = Join-Path $RepoRoot "src"

    if (Test-Path -LiteralPath $sourceRoot) {
        Get-ChildItem -LiteralPath $sourceRoot -Recurse -File -Filter "*.cs" | ForEach-Object {
            $scanPaths.Add($_.FullName) | Out-Null
        }
    }

    foreach ($relativePath in @("README.md", "CHANGELOG_FIX_NOTES.md")) {
        $path = Join-Path $RepoRoot $relativePath

        if (Test-Path -LiteralPath $path) {
            $scanPaths.Add($path) | Out-Null
        }
    }

    if ($scanPaths.Count -eq 0) {
        throw "Forbidden-term scan did not find any files to scan."
    }

    $pattern = "CSO v1|CSO v2|--format"
    $matches = Select-String -LiteralPath $scanPaths.ToArray() -Pattern $pattern -ErrorAction SilentlyContinue

    if ($matches) {
        $matches | Select-Object Path, LineNumber, Line | Format-Table -AutoSize | Out-String | Write-Host
        throw "Forbidden user-facing terms were found."
    }
}

function Test-HelpSmoke {
    $output = (& dotnet run --project $CliProject --no-restore -- --help) 2>&1
    $exitCode = $LASTEXITCODE
    $text = ($output | Out-String)

    Write-Host $text.Trim()

    if ($exitCode -ne 0) {
        throw "Help smoke failed with exit code $exitCode."
    }

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
            throw "Help output does not contain required text: $required"
        }
    }
}

function Test-JsonArgumentSmoke {
    $output = (& dotnet run --project $CliProject --no-restore -- compress "missing.iso" --measure --profile bad --json) 2>&1
    $exitCode = $LASTEXITCODE
    $text = ($output | Out-String).Trim()

    Write-Host $text

    if ($exitCode -ne 2) {
        throw "Invalid profile JSON smoke expected exit code 2, got $exitCode."
    }

    $json = $text | ConvertFrom-Json

    if ($json.schemaVersion -ne 1) {
        throw "Invalid profile JSON smoke did not return schemaVersion = 1."
    }

    if ($json.success -ne $false) {
        throw "Invalid profile JSON smoke did not report success = false."
    }

    if ($json.error.code -ne "InvalidArguments") {
        throw "Invalid profile JSON smoke returned unexpected error code: $($json.error.code)"
    }

    if ($json.error.message -notmatch "Supported profiles: game-safe\|compat\|fast\|smallest\|archive-smallest") {
        throw "Invalid profile JSON smoke returned an unexpected message: $($json.error.message)"
    }
}

function Invoke-ScriptFile {
    param(
        [string]$ScriptPath,
        [hashtable]$NamedArguments,
        [string]$StepName
    )

    if (-not (Test-Path -LiteralPath $ScriptPath)) {
        throw "Required script was not found: $ScriptPath"
    }

    & $ScriptPath @NamedArguments

    if ($LASTEXITCODE -ne 0) {
        throw "$StepName failed with exit code $LASTEXITCODE."
    }
}

function Copy-NativeRuntime {
    param(
        [string]$SourcePath,
        [string]$DestinationPath,
        [string]$Context
    )

    if (-not (Test-Path -LiteralPath $SourcePath -PathType Leaf)) {
        throw "Native DLL was not found for $Context`: $SourcePath"
    }

    $destinationDirectory = Split-Path -Parent $DestinationPath

    New-Item `
        -ItemType Directory `
        -Force `
        -Path $destinationDirectory |
        Out-Null

    Copy-Item `
        -LiteralPath $SourcePath `
        -Destination $DestinationPath `
        -Force

    if (-not (Test-Path -LiteralPath $DestinationPath -PathType Leaf)) {
        throw "Native DLL was not staged for $Context`: $DestinationPath"
    }

    $sourceHash = (
        Get-FileHash -LiteralPath $SourcePath -Algorithm SHA256
    ).Hash

    $destinationHash = (
        Get-FileHash -LiteralPath $DestinationPath -Algorithm SHA256
    ).Hash

    if ($sourceHash -ne $destinationHash) {
        throw "Native DLL hash mismatch for $Context."
    }
}

$RepoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
$SolutionPath = Join-Path $RepoRoot "CsoKit.slnx"
$CliProject = Join-Path $RepoRoot "src\CsoKit.Cli\CsoKit.Cli.csproj"
$RoundtripGateScript = Join-Path $RepoRoot "scripts\Run-RoundtripGate.ps1"
$ProfileMatrixScript = Join-Path $RepoRoot "scripts\Run-ProfileRoundtripMatrix.ps1"
$NativeBuildScript = Join-Path $RepoRoot "scripts\Build-Native.ps1"
$NativeDllPath = Join-Path $RepoRoot "artifacts\native-build\win-x64\Release\CsoKit.Native.dll"
$NativeTestDllPath = Join-Path $RepoRoot "tests\CsoKit.Tests\bin\$Configuration\net10.0\CsoKit.Native.dll"

if (-not (Test-Path -LiteralPath $SolutionPath)) {
    throw "Solution file was not found: $SolutionPath"
}

if (-not (Test-Path -LiteralPath $CliProject)) {
    throw "CLI project was not found: $CliProject"
}

$ResolvedInputIso = $null

if (-not $SkipRealIsoGates) {
    $ResolvedInputIso = Get-ResolvedIsoPath -Path $InputIso
}

Write-GateHeader -Title "CsoKit Release Gate"
Write-Host "Repo:          $RepoRoot"
Write-Host "Configuration: $Configuration"
Write-Host "Real ISO:      $(if ($SkipRealIsoGates) { 'skipped' } else { $ResolvedInputIso })"
Write-Host "Artifacts:     $(if ($KeepArtifacts) { 'kept' } else { 'deleted on success' })"

Invoke-GateStep -Name "dotnet restore" -Action {
    Invoke-DotNet -StepName "dotnet restore" -Arguments @(
        "restore",
        $SolutionPath,
        "-p:NuGetAudit=false"
    )
}

Invoke-GateStep -Name "native codec build" -Action {
    Invoke-ScriptFile -ScriptPath $NativeBuildScript -NamedArguments @{
        Configuration = "Release"
        Platform = "x64"
    } -StepName "native codec build"
}

Invoke-GateStep -Name "dotnet build" -Action {
    Invoke-DotNet -StepName "dotnet build" -Arguments @(
        "build",
        $SolutionPath,
        "-c",
        $Configuration,
        "--no-restore",
        "-p:NuGetAudit=false"
    )
}

Invoke-GateStep -Name "stage native backend for tests" -Action {
    Copy-NativeRuntime `
        -SourcePath $NativeDllPath `
        -DestinationPath $NativeTestDllPath `
        -Context "$Configuration tests"
}

Invoke-GateStep -Name "dotnet test" -Action {
    Invoke-DotNet -StepName "dotnet test" -Arguments @(
        "test",
        $SolutionPath,
        "-c",
        $Configuration,
        "--no-build",
        "-p:NuGetAudit=false"
    )
}

Invoke-GateStep -Name "forbidden terms scan" -Action {
    Test-ForbiddenTerms
}

Invoke-GateStep -Name "help smoke" -Action {
    Test-HelpSmoke
}

Invoke-GateStep -Name "JSON argument smoke" -Action {
    Test-JsonArgumentSmoke
}

if (-not $SkipRealIsoGates) {
    Invoke-GateStep -Name "real roundtrip gate" -Action {
        $arguments = @{
            InputIso = $ResolvedInputIso
        }

        if ($KeepArtifacts) {
            $arguments["KeepArtifacts"] = $true
        }

        if ($Quiet) {
            $arguments["Quiet"] = $true
        }

        Invoke-ScriptFile -ScriptPath $RoundtripGateScript -NamedArguments $arguments -StepName "real roundtrip gate"
    }

    Invoke-GateStep -Name "profile roundtrip matrix" -Action {
        $arguments = @{
            InputIso = $ResolvedInputIso
        }

        if ($KeepArtifacts) {
            $arguments["KeepArtifacts"] = $true
        }

        if ($Quiet) {
            $arguments["Quiet"] = $true
        }

        Invoke-ScriptFile -ScriptPath $ProfileMatrixScript -NamedArguments $arguments -StepName "profile roundtrip matrix"
    }
}
else {
    Write-Host ""
    Write-Host "[real ISO gates] SKIPPED"
}

Write-Host ""
Write-Host "Release gate result"
Write-Host "-------------------"
Write-Host "Status: PASS"

$global:LASTEXITCODE = 0
