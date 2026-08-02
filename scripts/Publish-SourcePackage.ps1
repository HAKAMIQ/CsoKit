[CmdletBinding()]
param(
    [string]$Version = ""
)

$ErrorActionPreference = "Stop"

function Get-RelativePathCompat {
    param(
        [Parameter(Mandatory = $true)]
        [string]$BasePath,

        [Parameter(Mandatory = $true)]
        [string]$FullPath
    )

    $baseFullPath = [System.IO.Path]::GetFullPath($BasePath).TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
    $itemFullPath = [System.IO.Path]::GetFullPath($FullPath)

    $baseUri = [System.Uri]::new($baseFullPath)
    $itemUri = [System.Uri]::new($itemFullPath)
    $relativeUri = $baseUri.MakeRelativeUri($itemUri)
    $relativePath = [System.Uri]::UnescapeDataString($relativeUri.ToString())

    return $relativePath.Replace('/', [System.IO.Path]::DirectorySeparatorChar)
}

function Assert-RequiredSourceFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepoRoot,

        [Parameter(Mandatory = $true)]
        [string]$RelativePath
    )

    $fullPath = Join-Path $RepoRoot $RelativePath

    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
        throw "Required source file is missing: $RelativePath"
    }
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$versionFile = Join-Path $repoRoot "VERSION"
if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = (Get-Content -LiteralPath $versionFile -Raw).Trim()
}
if ([string]::IsNullOrWhiteSpace($Version)) {
    throw "VERSION is empty."
}
$artifactsDir = Join-Path $repoRoot "artifacts"
$sourceDir = Join-Path $artifactsDir "source"
$zipPath = Join-Path $sourceDir "csokit-$Version-source.zip"
$stagingDir = Join-Path $sourceDir "staging"

$blockedTopLevel = @(".git", ".vs", "bin", "obj", "artifacts", "TestResults")
$blockedNested = @("bin", "obj", "TestResults")

$requiredSourceFiles = @(
    "VERSION",
    "Directory.Build.props",
    "CsoKit.slnx",
    "src\CsoKit.Core\CsoKit.Core.csproj",
    "src\CsoKit.Application\CsoKit.Application.csproj",
    "src\CsoKit.Cli\CsoKit.Cli.csproj",
    "src\CsoKit.App\CsoKit.App.csproj",
    "tests\CsoKit.Tests\CsoKit.Tests.csproj",
    "tests\CsoKit.App.Tests\CsoKit.App.Tests.csproj",
    "scripts\Verify-Hardening.ps1",
    "native\CsoKit.Native\CMakeLists.txt",
    "native\CsoKit.Native\src\csokit_native.cpp",
    "native\CsoKit.Native\include\csokit_native.h",
    "native\CsoKit.Native\include\csokit_version.h.in",
    "native\third_party\zopfli\src\zopfli\zopfli_lib.c"
)

Write-Host "CsoKit Source Package Publisher"
Write-Host "Version: $Version"
Write-Host "Repo:    $repoRoot"
Write-Host ""

foreach ($relativePath in $requiredSourceFiles) {
    Assert-RequiredSourceFile -RepoRoot $repoRoot -RelativePath $relativePath
}

Remove-Item $sourceDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $stagingDir | Out-Null

$items = Get-ChildItem $repoRoot -Force -Recurse -File | Where-Object {
    $relative = Get-RelativePathCompat -BasePath $repoRoot -FullPath $_.FullName
    $parts = $relative -split '[\\/]'

    if ($parts.Count -eq 0) {
        return $false
    }

    if ($blockedTopLevel -contains $parts[0]) {
        return $false
    }

    if ($parts.Count -eq 1 -and [System.IO.Path]::GetExtension($parts[0]) -eq ".zip") {
        return $false
    }

    foreach ($part in $parts) {
        if ($blockedNested -contains $part) {
            return $false
        }
    }

    return $true
}

$relativeItems = @(
    $items |
        Sort-Object FullName |
        ForEach-Object { Get-RelativePathCompat -BasePath $repoRoot -FullPath $_.FullName }
)

foreach ($relative in $relativeItems) {
    $sourcePath = Join-Path $repoRoot $relative
    $destinationPath = Join-Path $stagingDir $relative
    $destinationDir = Split-Path $destinationPath -Parent

    New-Item -ItemType Directory -Force $destinationDir | Out-Null
    Copy-Item -LiteralPath $sourcePath -Destination $destinationPath -Force
}

$sourceListPath = Join-Path $stagingDir "SOURCE_FILES.txt"
$relativeItems |
    ForEach-Object { $_.Replace([System.IO.Path]::DirectorySeparatorChar, '/') } |
    Set-Content -LiteralPath $sourceListPath -Encoding UTF8

$hashManifestPath = Join-Path $stagingDir "SOURCE-MANIFEST.sha256"
Get-ChildItem -LiteralPath $stagingDir -File -Recurse |
    Where-Object { $_.FullName -ne $hashManifestPath } |
    Sort-Object FullName |
    ForEach-Object {
        $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        $relative = Get-RelativePathCompat -BasePath $stagingDir -FullPath $_.FullName
        $normalized = $relative.Replace([System.IO.Path]::DirectorySeparatorChar, '/')
        "$hash  $normalized"
    } |
    Set-Content -LiteralPath $hashManifestPath -Encoding UTF8

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

Compress-Archive -Path (Join-Path $stagingDir "*") -DestinationPath $zipPath -Force
Remove-Item -LiteralPath $stagingDir -Recurse -Force

if (-not (Test-Path -LiteralPath $zipPath -PathType Leaf)) {
    throw "Source package was not produced: $zipPath"
}

Write-Host "[PASS] Source package created"
Write-Host "ZipPath: $zipPath"
