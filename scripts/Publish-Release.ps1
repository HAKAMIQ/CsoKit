[CmdletBinding()]
param(
    [string]$Version = "",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "OutputFileNamePolicy.ps1")

function Invoke-Checked {
    param(
        [string]$StepName,
        [scriptblock]$Command
    )

    & $Command

    if ($LASTEXITCODE -ne 0) {
        throw "$StepName failed with exit code $LASTEXITCODE."
    }
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
    return [System.Uri]::UnescapeDataString($relativeUri.ToString()).Replace('\', '/')
}

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$versionFile = Join-Path $RepoRoot "VERSION"

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = (Get-Content -LiteralPath $versionFile -Raw).Trim()
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    throw "VERSION is empty."
}

$SolutionFile = Get-ChildItem -Path $RepoRoot -File |
    Where-Object { $_.Extension -in ".sln", ".slnx" } |
    Select-Object -First 1

if (-not $SolutionFile) {
    throw "No .sln or .slnx file was found in repo root: $RepoRoot"
}

if ($Runtime -ne "win-x64") {
    throw "Native backend packaging currently supports win-x64 only. Runtime requested: $Runtime"
}

$Solution = $SolutionFile.FullName
$CliProject = Join-Path $RepoRoot "src\CsoKit.Cli\CsoKit.Cli.csproj"
$ArtifactsDir = Join-Path $RepoRoot "artifacts"
$PublishDir = Join-Path (Join-Path $ArtifactsDir "publish") $Runtime
$ReleaseDir = Join-Path $ArtifactsDir "release"
$ZipPath = Join-Path $ReleaseDir "csokit-$Version-$Runtime.zip"
$NativeDllPath = Join-Path $ArtifactsDir "native-build\win-x64\Release\CsoKit.Native.dll"
$TestNativeDllPath = Join-Path $RepoRoot "tests\CsoKit.Tests\bin\Release\net10.0\CsoKit.Native.dll"

Write-Host "CsoKit Release Publisher"
Write-Host "Version:  $Version"
Write-Host "Runtime:  $Runtime"
Write-Host "Solution: $Solution"
Write-Host ""

Remove-Item $ArtifactsDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $PublishDir | Out-Null
New-Item -ItemType Directory -Force $ReleaseDir | Out-Null

Write-Host "[1/9] Restore"
Invoke-Checked "Restore" {
    dotnet restore $Solution -r $Runtime -p:NuGetAudit=false
}

Write-Host "[2/9] Build native backend"
& "$PSScriptRoot\Build-Native.ps1" -Configuration Release -Platform x64

if (-not (Test-Path -LiteralPath $NativeDllPath -PathType Leaf)) {
    throw "Native DLL was not produced: $NativeDllPath"
}

Write-Host "[3/9] Build Release"
Invoke-Checked "Build Release" {
    dotnet build $Solution -c Release --no-restore -p:NuGetAudit=false -p:Version=$Version
}

Write-Host "[4/9] Stage native backend and test Release"
New-Item -ItemType Directory -Force (Split-Path -Parent $TestNativeDllPath) | Out-Null
Copy-Item -LiteralPath $NativeDllPath -Destination $TestNativeDllPath -Force
Invoke-Checked "Test Release with native integration" {
    dotnet test $Solution -c Release --no-build
}

Write-Host "[5/9] Publish single-file CLI"
Invoke-Checked "Publish CLI" {
    dotnet publish $CliProject `
        -c Release `
        -r $Runtime `
        --self-contained true `
        -o $PublishDir `
        -p:PublishSingleFile=true `
        -p:EnableCompressionInSingleFile=true `
        -p:PublishTrimmed=false `
        -p:DebugType=None `
        -p:DebugSymbols=false `
        -p:Version=$Version `
        -p:NuGetAudit=false
}

$ExePath = Join-Path $PublishDir "csokit.exe"
$PublishNativeDllPath = Join-Path $PublishDir "CsoKit.Native.dll"

if (-not (Test-Path -LiteralPath $ExePath -PathType Leaf)) {
    throw "Published executable was not found: $ExePath"
}

Write-Host "[6/9] Copy runtime dependencies and notices"
Copy-Item -LiteralPath $NativeDllPath -Destination $PublishNativeDllPath -Force
Copy-Item (Join-Path $RepoRoot "README.md") (Join-Path $PublishDir "README.md") -Force
Copy-Item (Join-Path $RepoRoot "LICENSE.txt") (Join-Path $PublishDir "LICENSE.txt") -Force
Copy-Item (Join-Path $RepoRoot "THIRD_PARTY_NOTICES.md") (Join-Path $PublishDir "THIRD_PARTY_NOTICES.md") -Force

$ReleaseNotesPath = Join-Path $RepoRoot "RELEASE_NOTES.md"
if (Test-Path -LiteralPath $ReleaseNotesPath -PathType Leaf) {
    Copy-Item $ReleaseNotesPath (Join-Path $PublishDir "RELEASE_NOTES.md") -Force
}

Write-Host "[7/9] CLI and native capability smoke"
$versionText = ((& $ExePath --version) | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or $versionText -notmatch [regex]::Escape($Version)) {
    throw "Version smoke test failed. Output: $versionText"
}

$helpText = ((& $ExePath --help) | Out-String).Trim()
if ($LASTEXITCODE -ne 0) {
    throw "Help smoke test failed."
}

foreach ($required in @("info", "verify", "repair", "analyze", "detect", "decompress", "compress", "codecs", "native-info", "--json", "--quiet", "--profile", "--threads", "--block", "--zopfli", "--codec-report", "game-safe|compat|fast|smallest|archive-smallest")) {
    if ($helpText -notmatch [regex]::Escape($required)) {
        throw "Help output does not contain required text: $required"
    }
}

$nativeInfoOutput = ((& $ExePath native-info) | Out-String).Trim()
if ($LASTEXITCODE -ne 0) {
    throw "native-info smoke test failed."
}

foreach ($requiredPattern in @(
    "Backend:\s+native",
    "Native available:\s+True",
    "ABI\s+2",
    "Native zlib:\s+available",
    "Native libdeflate:\s+available",
    "Native Zopfli:\s+available")) {
    if ($nativeInfoOutput -notmatch $requiredPattern) {
        Write-Host $nativeInfoOutput
        throw "native-info did not report required state: $requiredPattern"
    }
}

Write-Host "[8/9] Published native round-trip"
$smokeRoot = Join-Path $ArtifactsDir "published-native-roundtrip"
$inputIso = Join-Path $smokeRoot "input.iso"
$outputCso = Join-Path $smokeRoot "smoke.cso"
$restoredIso = Join-Path $smokeRoot "back.iso"
Assert-CsoKitOutputFileName -Path $outputCso -Context "Published native round-trip CSO"
Assert-CsoKitOutputFileName -Path $restoredIso -Context "Published native round-trip restored ISO"
Remove-Item -LiteralPath $smokeRoot -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $smokeRoot | Out-Null

$sample = [byte[]]::new(65536)
for ($index = 0; $index -lt $sample.Length; $index++) {
    $sample[$index] = [byte](($index * 17) % 251)
}
[System.IO.File]::WriteAllBytes($inputIso, $sample)

try {
    & $ExePath compress $inputIso -o $outputCso --profile game-safe --threads 1 --block 2048 --zopfli --deep-verify
    if ($LASTEXITCODE -ne 0) {
        throw "Published native compression round-trip failed during compression."
    }

    & $ExePath decompress $outputCso -o $restoredIso
    if ($LASTEXITCODE -ne 0) {
        throw "Published native compression round-trip failed during decompression."
    }

    $inputHash = (Get-FileHash -LiteralPath $inputIso -Algorithm SHA256).Hash
    $restoredHash = (Get-FileHash -LiteralPath $restoredIso -Algorithm SHA256).Hash
    if ($inputHash -ne $restoredHash) {
        throw "Published native round-trip SHA256 mismatch."
    }
}
finally {
    Remove-Item -LiteralPath $smokeRoot -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "[9/9] SHA256 manifest and ZIP"
$ManifestPath = Join-Path $PublishDir "SHA256SUMS.txt"
Get-ChildItem $PublishDir -File -Recurse |
    Where-Object { $_.Name -ne "SHA256SUMS.txt" } |
    Sort-Object FullName |
    ForEach-Object {
        $hash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        $relative = Get-RelativePathCompat -BasePath $PublishDir -FullPath $_.FullName
        "$hash  $relative"
    } |
    Set-Content $ManifestPath -Encoding UTF8

if (Test-Path $ZipPath) {
    Remove-Item $ZipPath -Force
}

Compress-Archive -Path (Join-Path $PublishDir "*") -DestinationPath $ZipPath -Force

Write-Host ""
Write-Host "[PASS] Release package created"
Write-Host "PublishDir: $PublishDir"
Write-Host "ZipPath:    $ZipPath"
