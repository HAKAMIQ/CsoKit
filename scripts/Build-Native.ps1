[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64"
)

$ErrorActionPreference = "Stop"

function Find-VsDevCmd {
    $candidates = [System.Collections.Generic.List[string]]::new()

    $programFilesX86 = ${env:ProgramFiles(x86)}
    if (-not [string]::IsNullOrWhiteSpace($programFilesX86)) {
        $vswherePath = Join-Path $programFilesX86 "Microsoft Visual Studio\Installer\vswhere.exe"

        if (Test-Path -LiteralPath $vswherePath) {
            $installationPaths = @(
                & $vswherePath -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
                & $vswherePath -latest -products * -property installationPath
            ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique

            foreach ($path in $installationPaths) {
                $candidates.Add((Join-Path $path "Common7\Tools\VsDevCmd.bat"))
            }
        }
    }

    $roots = @(
        (Join-Path $env:ProgramFiles "Microsoft Visual Studio"),
        (Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio")
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) -and (Test-Path -LiteralPath $_) }

    foreach ($root in $roots) {
        Get-ChildItem -LiteralPath $root -Directory -ErrorAction SilentlyContinue |
            ForEach-Object {
                Get-ChildItem -LiteralPath $_.FullName -Directory -ErrorAction SilentlyContinue |
                    ForEach-Object {
                        $candidates.Add((Join-Path $_.FullName "Common7\Tools\VsDevCmd.bat"))
                    }
            }
    }

    foreach ($candidate in ($candidates | Select-Object -Unique)) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return $candidate
        }
    }

    return $null
}

function Assert-NativeSourceLayout {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepoRoot
    )

    $requiredFiles = @(
        "native\CsoKit.Native\CMakeLists.txt",
        "native\CsoKit.Native\src\csokit_native.cpp",
        "native\CsoKit.Native\include\csokit_native.h",
        "native\third_party\zopfli\src\zopfli\zopfli_lib.c"
    )

    foreach ($relativePath in $requiredFiles) {
        $fullPath = Join-Path $RepoRoot $relativePath

        if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
            throw "Native source layout is incomplete. Missing: $relativePath"
        }
    }
}

function Invoke-CMakeBuild {
    param(
        [Parameter(Mandatory = $true)]
        [string]$CMakeExe,

        [Parameter(Mandatory = $true)]
        [string]$NativeRoot,

        [Parameter(Mandatory = $true)]
        [string]$BuildDir,

        [Parameter(Mandatory = $true)]
        [string]$Configuration
    )

    & $CMakeExe -S $NativeRoot -B $BuildDir -A x64
    if ($LASTEXITCODE -ne 0) {
        return $false
    }

    & $CMakeExe --build $BuildDir --config $Configuration
    return $LASTEXITCODE -eq 0
}

if ($Configuration -ne "Debug" -and $Configuration -ne "Release") {
    throw "Unsupported configuration: $Configuration"
}

if ($Platform -ne "x64") {
    throw "Unsupported platform: $Platform"
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$nativeRoot = Join-Path $repoRoot "native\CsoKit.Native"
$buildDir = Join-Path $repoRoot "artifacts\native-build\win-x64"

Write-Host "CsoKit Native Builder"
Write-Host "Configuration: $Configuration"
Write-Host "Platform:      $Platform"
Write-Host ""

Assert-NativeSourceLayout -RepoRoot $repoRoot

$cmakeBin = "C:\Program Files\CMake\bin"
if (Test-Path -LiteralPath $cmakeBin) {
    $env:Path = "$cmakeBin;$env:Path"
}

$cmakeCommand = Get-Command cmake.exe -ErrorAction SilentlyContinue
if (-not $cmakeCommand) {
    throw "cmake.exe was not found on PATH."
}

Write-Host "CMake: $($cmakeCommand.Source)"
Write-Host ""

Remove-Item -LiteralPath $buildDir -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "[1/2] Build native backend using CMake"
$directBuildSucceeded = Invoke-CMakeBuild `
    -CMakeExe $cmakeCommand.Source `
    -NativeRoot $nativeRoot `
    -BuildDir $buildDir `
    -Configuration $Configuration

if (-not $directBuildSucceeded) {
    Write-Host ""
    Write-Host "Direct CMake build failed. Trying Visual Studio Developer Command Prompt fallback."

    Remove-Item -LiteralPath $buildDir -Recurse -Force -ErrorAction SilentlyContinue

    $vsDevCmd = Find-VsDevCmd
    if ([string]::IsNullOrWhiteSpace($vsDevCmd)) {
        throw "Visual Studio Developer Command Prompt was not found. Install Visual Studio with C++ build tools."
    }

    Write-Host "VsDevCmd: $vsDevCmd"
    Write-Host ""

    $cmdLine = '"' + $vsDevCmd + '" -arch=amd64 -host_arch=amd64' +
        ' && "' + $cmakeCommand.Source + '" -S "' + $nativeRoot + '" -B "' + $buildDir + '" -A x64' +
        ' && "' + $cmakeCommand.Source + '" --build "' + $buildDir + '" --config ' + $Configuration

    cmd.exe /d /s /c $cmdLine

    if ($LASTEXITCODE -ne 0) {
        throw "Native build failed with exit code $LASTEXITCODE."
    }
}

$dllPath = Join-Path $buildDir "$Configuration\CsoKit.Native.dll"

if (-not (Test-Path -LiteralPath $dllPath -PathType Leaf)) {
    throw "Native DLL was not produced: $dllPath"
}

Write-Host "[2/2] Validate native DLL"
Write-Host ""
Write-Host "[PASS] Native DLL built"
Write-Host "DLL: $dllPath"
