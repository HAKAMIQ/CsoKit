[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$Solution = Join-Path $RepoRoot "CsoKit.slnx"
$NativeDll = Join-Path $RepoRoot "artifacts\native-build\win-x64\Release\CsoKit.Native.dll"
$TestOutput = Join-Path $RepoRoot "tests\CsoKit.Tests\bin\$Configuration\net10.0\CsoKit.Native.dll"

function Invoke-Checked {
    param([string]$Name, [scriptblock]$Command)
    Write-Host "[RUN] $Name"
    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "$Name failed with exit code $LASTEXITCODE."
    }
}

if (-not (Test-Path -LiteralPath $Solution -PathType Leaf)) {
    throw "Solution was not found: $Solution"
}


Write-Host "[RUN] Architecture source guards"
$CliProject = Join-Path $RepoRoot "src\CsoKit.Cli\CsoKit.Cli.csproj"
$CliSourceRoot = Join-Path $RepoRoot "src\CsoKit.Cli"
$ApplicationSourceRoot = Join-Path $RepoRoot "src\CsoKit.Application"

$cliProjectText = Get-Content -LiteralPath $CliProject -Raw
if ($cliProjectText -match 'ProjectReference[^>]+CsoKit\.Core') {
    throw "CLI must reference CsoKit.Application instead of CsoKit.Core directly."
}

$forbiddenCliUseCasePatterns = @(
    'new\s+CsoCompressor\s*\(',
    'new\s+CsoRepairer\s*\(',
    'new\s+CsoVerifier\s*\(',
    'new\s+CsoDeepVerifier\s*\(',
    'ContainerDeepVerifier\.Verify\s*\('
)

$cliSourceText = (Get-ChildItem -LiteralPath $CliSourceRoot -Filter *.cs -Recurse -File |
    ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }) -join "`n"

foreach ($pattern in $forbiddenCliUseCasePatterns) {
    if ($cliSourceText -match $pattern) {
        throw "CLI bypasses CsoKit.Application with forbidden use-case pattern: $pattern"
    }
}

$legacyDetailParser = Join-Path $ApplicationSourceRoot "CsoOperationDetailParser.cs"
if (Test-Path -LiteralPath $legacyDetailParser) {
    throw "Legacy text detail parser must not exist: $legacyDetailParser"
}

$applicationSourceText = (Get-ChildItem -LiteralPath $ApplicationSourceRoot -Filter *.cs -Recurse -File |
    ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }) -join "`n"
if ($applicationSourceText -match 'CsoOperationDetailParser') {
    throw "Application results must originate from typed detail records, not parsed report text."
}

Write-Host "[PASS] Architecture source guards"


Write-Host "[RUN] Release script output-name guards"
$OutputFileNamePolicy = Join-Path $PSScriptRoot "OutputFileNamePolicy.ps1"
if (-not (Test-Path -LiteralPath $OutputFileNamePolicy -PathType Leaf)) {
    throw "Output filename policy helper was not found: $OutputFileNamePolicy"
}

. $OutputFileNamePolicy

foreach ($validName in @("ab.cso", "1234567890.iso", "éx.cso")) {
    Assert-CsoKitOutputFileName -Path $validName -Context "Output filename policy self-test"
}

foreach ($invalidName in @(("x" + ".cso"), ("1234567890" + "1" + ".iso"))) {
    $wasRejected = $false

    try {
        Assert-CsoKitOutputFileName -Path $invalidName -Context "Output filename policy negative self-test"
    }
    catch {
        $wasRejected = $true
    }

    if (-not $wasRejected) {
        throw "Output filename policy accepted an invalid self-test name: $invalidName"
    }
}

$guardedReleaseScripts = @(
    "Publish-Release.ps1",
    "Run-PublishedExeSmoke.ps1",
    "Run-RoundtripGate.ps1",
    "Run-ProfileRoundtripMatrix.ps1"
)

foreach ($scriptName in $guardedReleaseScripts) {
    $scriptPath = Join-Path $PSScriptRoot $scriptName
    $scriptText = Get-Content -LiteralPath $scriptPath -Raw

    if ($scriptText -notmatch 'OutputFileNamePolicy\.ps1') {
        throw "Release script does not load the output filename guard: $scriptName"
    }

    if ($scriptText -notmatch 'Assert-CsoKitOutputFileName') {
        throw "Release script does not validate generated output names: $scriptName"
    }
}

$quotedOutputPattern = '["''](?<path>[^"'']+\.(?:cso|iso))["'']'
Get-ChildItem -LiteralPath $PSScriptRoot -Filter *.ps1 -File | ForEach-Object {
    $scriptPath = $_.FullName
    $scriptText = Get-Content -LiteralPath $scriptPath -Raw

    foreach ($match in [regex]::Matches($scriptText, $quotedOutputPattern, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)) {
        $literalPath = $match.Groups['path'].Value

        if ($literalPath.Contains('$')) {
            continue
        }

        Assert-CsoKitOutputFileName -Path $literalPath -Context "PowerShell literal in $($_.Name)"
    }
}

Write-Host "[PASS] Release script output-name guards"

Write-Host "[RUN] Published EXE smoke source guards"
$PublishedSmokePath = Join-Path $PSScriptRoot "Run-PublishedExeSmoke.ps1"
$publishedSmokeText = Get-Content -LiteralPath $PublishedSmokePath -Raw

foreach ($requiredSmokePattern in @(
    'csokit verify <input\.cso\|input\.zso\|input\.dax>',
    'Build-Native\.ps1',
    'CsoKit\.Native\.dll',
    'Copy-Item\s+-LiteralPath\s+\$NativeDllPath\s+-Destination\s+\$PublishNativeDllPath'
)) {
    if ($publishedSmokeText -notmatch $requiredSmokePattern) {
        throw "Published EXE smoke is missing required contract or native staging pattern: $requiredSmokePattern"
    }
}

$verifyScriptText = Get-Content -LiteralPath $PSCommandPath -Raw
if ($verifyScriptText -notmatch 'Run-PublishedExeSmoke\.ps1') {
    throw "Verify-Hardening must execute Run-PublishedExeSmoke.ps1."
}

Write-Host "[PASS] Published EXE smoke source guards"


Invoke-Checked "Restore" {
    dotnet restore $Solution -r $Runtime -p:NuGetAudit=false
}

Write-Host "[RUN] Build native backend"
& (Join-Path $PSScriptRoot "Build-Native.ps1") -Configuration Release -Platform x64
if (-not (Test-Path -LiteralPath $NativeDll -PathType Leaf)) {
    throw "Native DLL was not produced: $NativeDll"
}

Invoke-Checked "Build Debug" {
    dotnet build $Solution -c Debug --no-restore -p:NuGetAudit=false
}

Invoke-Checked "Build Release" {
    dotnet build $Solution -c Release --no-restore -p:NuGetAudit=false
}

New-Item -ItemType Directory -Force (Split-Path -Parent $TestOutput) | Out-Null
Copy-Item -LiteralPath $NativeDll -Destination $TestOutput -Force

Invoke-Checked "Tests with native integration" {
    dotnet test $Solution -c $Configuration --no-build
}

Write-Host "[RUN] Published EXE smoke"
& (Join-Path $PSScriptRoot "Run-PublishedExeSmoke.ps1") `
    -Configuration $Configuration `
    -Runtime $Runtime `
    -SkipRealIsoGates `
    -Quiet

Write-Host "[RUN] Publish and verify release"
& (Join-Path $PSScriptRoot "Publish-Release.ps1") -Runtime $Runtime
& (Join-Path $PSScriptRoot "Verify-Release.ps1") -Runtime $Runtime

Write-Host "[PASS] CsoKit hardening verification completed."
