[CmdletBinding()]
param(
    [string[]]$InputPath = @(),

    [string]$Root = "",

    # Accept either a normal PowerShell array or a comma-separated string.
    # This is intentional because pwsh -File cannot reliably pass array values
    # from Windows PowerShell callers.
    [string[]]$Profiles = @("game-safe"),

    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [int]$Threads = [Math]::Max(1, [Environment]::ProcessorCount),

    [string]$OutputDirectory = "",

    [switch]$UseZopfli,

    [switch]$KeepArtifacts,

    [switch]$Quiet
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$script:ValidBenchmarkProfiles = [string[]]@("game-safe", "compat", "fast", "smallest", "archive-smallest")

function Resolve-BenchmarkProfiles {
    param([string[]]$Values)

    $resolved = New-Object System.Collections.Generic.List[string]

    foreach ($value in @($Values)) {
        if ($null -eq $value) {
            continue
        }

        foreach ($part in ([string]$value -split ",")) {
            $profile = $part.Trim()

            if ([string]::IsNullOrWhiteSpace($profile)) {
                continue
            }

            if ($script:ValidBenchmarkProfiles -notcontains $profile) {
                throw "Unsupported profile '$profile'. Valid profiles: $($script:ValidBenchmarkProfiles -join ', ')."
            }

            if (-not $resolved.Contains($profile)) {
                $resolved.Add($profile) | Out-Null
            }
        }
    }

    if ($resolved.Count -eq 0) {
        $resolved.Add("game-safe") | Out-Null
    }

    return [string[]]$resolved.ToArray()
}

function Get-DefaultOutputDirectory {
    if (-not [string]::IsNullOrWhiteSpace($Root)) {
        $resolvedRoot = (Resolve-Path -LiteralPath $Root -ErrorAction Stop).ProviderPath
        return Join-Path $resolvedRoot "CsoKit_BenchmarkTruth"
    }

    foreach ($path in @($InputPath)) {
        if ([string]::IsNullOrWhiteSpace($path)) {
            continue
        }

        $resolvedPath = (Resolve-Path -LiteralPath $path -ErrorAction Stop).ProviderPath
        $item = Get-Item -LiteralPath $resolvedPath -ErrorAction Stop

        if ($item.PSIsContainer) {
            return Join-Path $item.FullName "CsoKit_BenchmarkTruth"
        }

        return Join-Path $item.Directory.FullName "CsoKit_BenchmarkTruth"
    }

    return Join-Path ([System.IO.Path]::GetTempPath()) ("CsoKit_BenchmarkTruth_" + [Guid]::NewGuid().ToString("N"))
}

function Write-Utf8NoBom {
    param(
        [string]$Path,
        [string]$Text
    )

    $encoding = [System.Text.UTF8Encoding]::new($false)
    [System.IO.File]::WriteAllText($Path, $Text, $encoding)
}

function Escape-MarkdownCell {
    param([object]$Value)

    if ($null -eq $Value) {
        return ""
    }

    return ([string]$Value).Replace("|", "\|").Replace("`r", " ").Replace("`n", " ")
}


function ConvertTo-ProcessArgumentString {
    param([string[]]$Arguments)

    $quoted = New-Object System.Collections.Generic.List[string]

    foreach ($argument in $Arguments) {
        if ($null -eq $argument) {
            $quoted.Add('""') | Out-Null
            continue
        }

        $text = [string]$argument

        if ($text.Length -eq 0) {
            $quoted.Add('""') | Out-Null
            continue
        }

        if ($text.IndexOfAny([char[]]@(' ', "`t", '"')) -lt 0) {
            $quoted.Add($text) | Out-Null
            continue
        }

        $builder = [System.Text.StringBuilder]::new()
        [void]$builder.Append('"')
        $slashCount = 0

        foreach ($character in $text.ToCharArray()) {
            if ($character -eq '\') {
                $slashCount++
                continue
            }

            if ($character -eq '"') {
                [void]$builder.Append((('\' * (($slashCount * 2) + 1)) -join ''))
                [void]$builder.Append('"')
                $slashCount = 0
                continue
            }

            if ($slashCount -gt 0) {
                [void]$builder.Append((('\' * $slashCount) -join ''))
                $slashCount = 0
            }

            [void]$builder.Append($character)
        }

        if ($slashCount -gt 0) {
            [void]$builder.Append((('\' * ($slashCount * 2)) -join ''))
        }

        [void]$builder.Append('"')
        $quoted.Add($builder.ToString()) | Out-Null
    }

    return ($quoted -join ' ')
}

function Get-JsonErrorCode {
    param([object]$Json)

    if ($null -eq $Json -or $null -eq $Json.error) {
        return $null
    }

    return [string]$Json.error.code
}

function Get-JsonErrorMessage {
    param([object]$Json)

    if ($null -eq $Json -or $null -eq $Json.error) {
        return $null
    }

    return [string]$Json.error.message
}

function New-BenchmarkStageFailure {
    param(
        [string]$Stage,
        [string]$Code,
        [string]$Message
    )

    return [pscustomobject]@{
        Stage = $Stage
        Code = $Code
        Message = $Message
    }
}

function Get-BenchmarkFailureText {
    param([object[]]$Failures)

    if ($null -eq $Failures -or $Failures.Count -eq 0) {
        return ""
    }

    return (($Failures | ForEach-Object {
        $stage = if ([string]::IsNullOrWhiteSpace([string]$_.Stage)) { "unknown" } else { [string]$_.Stage }
        $code = if ([string]::IsNullOrWhiteSpace([string]$_.Code)) { "Failure" } else { [string]$_.Code }
        $message = if ([string]::IsNullOrWhiteSpace([string]$_.Message)) { "Command failed." } else { [string]$_.Message }
        "$($stage)/$($code): $message"
    }) -join "; ")
}

function Get-ResolvedInputFiles {
    $extensions = @(".iso", ".cso", ".zso", ".dax", ".cso2")
    $files = New-Object System.Collections.Generic.List[System.IO.FileInfo]

    foreach ($path in $InputPath) {
        if ([string]::IsNullOrWhiteSpace($path)) {
            continue
        }

        $resolved = Resolve-Path -LiteralPath $path -ErrorAction Stop
        $item = Get-Item -LiteralPath $resolved.ProviderPath -ErrorAction Stop

        if ($item.PSIsContainer) {
            throw "InputPath must point to files. Use -Root for recursive corpus folders: $($item.FullName)"
        }

        $files.Add($item) | Out-Null
    }

    if (-not [string]::IsNullOrWhiteSpace($Root)) {
        $resolvedRoot = (Resolve-Path -LiteralPath $Root -ErrorAction Stop).ProviderPath

        if (-not (Test-Path -LiteralPath $resolvedRoot -PathType Container)) {
            throw "Root is not a directory: $Root"
        }

        Get-ChildItem -LiteralPath $resolvedRoot -File -Recurse |
            Where-Object { $extensions -contains $_.Extension.ToLowerInvariant() } |
            Sort-Object FullName |
            ForEach-Object { $files.Add($_) | Out-Null }
    }

    return $files |
        Sort-Object FullName -Unique
}

function Initialize-CsoTool {
    $script:RepoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).ProviderPath
    $solution = Join-Path $script:RepoRoot "CsoKit.slnx"
    $exe = Join-Path $script:RepoRoot "src/CsoKit.Cli/bin/$Configuration/net10.0/csokit.exe"
    $dll = Join-Path $script:RepoRoot "src/CsoKit.Cli/bin/$Configuration/net10.0/CsoKit.Cli.dll"

    if (-not (Test-Path -LiteralPath $exe -PathType Leaf) -and
        -not (Test-Path -LiteralPath $dll -PathType Leaf)) {
        if (-not $Quiet) {
            Write-Host "CLI binary was not found. Building $Configuration first..."
        }

        & dotnet build $solution -c $Configuration -p:NuGetAudit=false

        if ($LASTEXITCODE -ne 0) {
            throw "dotnet build failed with exit code $LASTEXITCODE."
        }
    }

    if (Test-Path -LiteralPath $exe -PathType Leaf) {
        $script:ToolFile = $exe
        $script:ToolPrefix = @()
        return
    }

    if (Test-Path -LiteralPath $dll -PathType Leaf) {
        $script:ToolFile = "dotnet"
        $script:ToolPrefix = @($dll)
        return
    }

    throw "CLI binary was not found after build."
}

function Invoke-ToolProcess {
    param([string[]]$Arguments)

    # Use PowerShell's native array invocation instead of ProcessStartInfo.
    # This avoids host-specific failures such as ArgumentList absence on Windows
    # PowerShell 5.1 and "Argument types do not match" binder errors on some
    # PowerShell 7 hosts, while still preserving paths with spaces.
    $allArgumentsList = New-Object System.Collections.Generic.List[string]

    foreach ($arg in @($script:ToolPrefix)) {
        if ($null -ne $arg) {
            $allArgumentsList.Add([string]$arg) | Out-Null
        }
    }

    foreach ($arg in @($Arguments)) {
        if ($null -ne $arg) {
            $allArgumentsList.Add([string]$arg) | Out-Null
        }
    }

    $commandArguments = [string[]]$allArgumentsList.ToArray()
    $timer = [System.Diagnostics.Stopwatch]::StartNew()
    $previousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = "Continue"

    try {
        $rawOutput = & $script:ToolFile @commandArguments 2>&1
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
        $timer.Stop()
    }

    $outputLines = New-Object System.Collections.Generic.List[string]
    foreach ($line in @($rawOutput)) {
        if ($null -eq $line) {
            continue
        }

        if ($line -is [System.Management.Automation.ErrorRecord]) {
            $outputLines.Add([string]$line.Exception.Message) | Out-Null
        }
        else {
            $outputLines.Add([string]$line) | Out-Null
        }
    }

    return [pscustomobject]@{
        ExitCode = [int]$exitCode
        Stdout = ($outputLines -join [Environment]::NewLine)
        Stderr = ""
        ElapsedMilliseconds = [Math]::Round($timer.Elapsed.TotalMilliseconds, 3)
        PeakWorkingSetBytes = 0L
    }
}

function Invoke-JsonTool {
    param([string[]]$Arguments)

    $process = Invoke-ToolProcess -Arguments $Arguments
    $json = $null
    $trimmedStdout = $process.Stdout.Trim()

    if (-not [string]::IsNullOrWhiteSpace($trimmedStdout)) {
        try {
            $json = $trimmedStdout | ConvertFrom-Json
        }
        catch {
            if ($process.ExitCode -eq 0) {
                throw "csokit did not return valid JSON for: $($Arguments -join ' ')"
            }
        }
    }

    if ($process.ExitCode -ne 0) {
        $code = Get-JsonErrorCode -Json $json
        $message = Get-JsonErrorMessage -Json $json

        if ([string]::IsNullOrWhiteSpace($message)) {
            $message = $process.Stderr.Trim()
        }

        if ([string]::IsNullOrWhiteSpace($message)) {
            $message = $trimmedStdout
        }

        if ([string]::IsNullOrWhiteSpace($code)) {
            $code = "ProcessExit$($process.ExitCode)"
        }

        throw "${code}: $message"
    }

    if ($null -eq $json) {
        throw "csokit did not return valid JSON for: $($Arguments -join ' ')"
    }

    return [pscustomobject]@{
        Json = $json
        Process = $process
    }
}

function ConvertTo-StringIntMap {
    param([object]$Value)

    $map = [ordered]@{}

    if ($null -eq $Value) {
        return $map
    }

    foreach ($property in $Value.PSObject.Properties) {
        $map[$property.Name] = [int]$property.Value
    }

    return $map
}

function Get-TopCodec {
    param([System.Collections.IDictionary]$CodecWins)

    if ($CodecWins.Count -eq 0) {
        return $null
    }

    if ($CodecWins.Count -eq 1) {
        return @($CodecWins.Keys)[0]
    }

    return "mixed"
}

function Get-Sha256Hex {
    param([string]$Path)

    return ((Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash).ToLowerInvariant()
}

function Get-ReportMarkdown {
    param([object]$Report)

    $builder = [System.Text.StringBuilder]::new()
    [void]$builder.AppendLine("# CsoKit Benchmark Truth Report")
    [void]$builder.AppendLine()
    [void]$builder.AppendLine("- Generated UTC: $($Report.generatedAtUtc)")
    [void]$builder.AppendLine("- Repo: $($Report.repo)")
    [void]$builder.AppendLine("- Profiles: $($Report.profiles -join ', ')")
    [void]$builder.AppendLine("- External comparison dependency: none")
    [void]$builder.AppendLine("- Corpus stored in Git: false")
    [void]$builder.AppendLine("- Output target: CSO1 game-safe normalization/compression")
    [void]$builder.AppendLine("- Cases passed: $($Report.casesPassed)")
    [void]$builder.AppendLine("- Cases skipped: $($Report.casesSkipped)")
    [void]$builder.AppendLine("- Cases failed: $($Report.casesFailed)")
    [void]$builder.AppendLine()
    [void]$builder.AppendLine("| Input | Profile | Format | Stage | Input bytes | Logical bytes | Output bytes | Saved % | Encode ms | Decode ms | Verify ms | Peak bytes | Stored blocks | Compressed blocks | Zero blocks | SHA256 match | Deep verify | Selected codec | Status | Reason |")
    [void]$builder.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|")

    foreach ($case in $Report.cases) {
        $status = if ($case.skipped) { "SKIP" } elseif ($case.success) { "PASS" } else { "FAIL" }
        $reason = if ($case.skipped) { $case.skippedReason } else { Get-BenchmarkFailureText -Failures @($case.failures) }
        [void]$builder.AppendLine(("| {0} | {1} | {2} | {3} | {4} | {5} | {6} | {7} | {8} | {9} | {10} | {11} | {12} | {13} | {14} | {15} | {16} | {17} | {18} | {19} |" -f
            (Escape-MarkdownCell $case.input),
            (Escape-MarkdownCell $case.profile),
            (Escape-MarkdownCell $case.format),
            (Escape-MarkdownCell $case.stage),
            $case.inputSizeBytes,
            $case.logicalSizeBytes,
            $case.outputSizeBytes,
            $case.savedPercent,
            $case.encodeMilliseconds,
            $case.decodeMilliseconds,
            $case.verifyMilliseconds,
            $case.peakWorkingSetBytes,
            $case.storedBlocks,
            $case.compressedBlocks,
            $case.zeroBlocks,
            $case.sha256Match,
            $case.deepVerifySuccess,
            (Escape-MarkdownCell $case.selectedCodec),
            $status,
            (Escape-MarkdownCell $reason)))
    }

    [void]$builder.AppendLine()
    [void]$builder.AppendLine("## Limits")
    [void]$builder.AppendLine()
    [void]$builder.AppendLine("- This report proves only the supplied local files and profiles.")
    [void]$builder.AppendLine("- CSO1 remains the safe output target.")
    [void]$builder.AppendLine("- Experimental or unavailable codecs are reported as candidates only when the CLI exposes them for the selected profile and flags.")
    [void]$builder.AppendLine("- A skipped case is an unsupported benchmark path, not failed compression evidence.")
    [void]$builder.AppendLine("- A failed case is evidence to investigate; the script does not produce a trusted output for failed cases.")

    return $builder.ToString()
}

Initialize-CsoTool

$Profiles = Resolve-BenchmarkProfiles -Values $Profiles

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Get-DefaultOutputDirectory
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

$artifactRoot = if ($KeepArtifacts) {
    Join-Path $OutputDirectory "artifacts"
}
else {
    Join-Path ([System.IO.Path]::GetTempPath()) ("CsoKit_BenchmarkArtifacts_" + [Guid]::NewGuid().ToString("N"))
}

New-Item -ItemType Directory -Path $artifactRoot -Force | Out-Null

$files = @(Get-ResolvedInputFiles)
$results = New-Object System.Collections.Generic.List[object]
$overallSuccess = $true

foreach ($file in $files) {
    foreach ($profile in $Profiles) {
        if (-not $Quiet) {
            Write-Host ("[benchmark] {0} [{1}]" -f $file.Name, $profile)
        }

        $caseId = [Guid]::NewGuid().ToString("N")
        $caseDir = Join-Path $artifactRoot $caseId
        New-Item -ItemType Directory -Path $caseDir -Force | Out-Null

        $warnings = New-Object System.Collections.Generic.List[string]
        $failures = New-Object System.Collections.Generic.List[object]
        $caseSuccess = $false
        $caseSkipped = $false
        $skippedReason = $null
        $stage = "detect"
        $format = $null
        $logicalSizeBytes = 0L
        $outputSizeBytes = 0L
        $savedPercent = 0.0
        $encodeMilliseconds = 0.0
        $decodeMilliseconds = 0.0
        $verifyMilliseconds = 0.0
        $inputVerifyMilliseconds = 0.0
        $peakWorkingSetBytes = 0L
        $sha256Match = $false
        $deepVerifySuccess = $false
        $sourceSha256 = $null
        $outputSha256 = $null
        $codecCandidates = @()
        $selectedCodecWins = [ordered]@{}
        $rejectedReasons = [ordered]@{}
        $selectedCodec = $null
        $compressedBlocks = 0
        $storedBlocks = 0
        $zeroBlocks = 0

        try {
            $stage = "detect"
            $detect = Invoke-JsonTool -Arguments @("detect", $file.FullName, "--json")
            $format = [string]$detect.Json.format

            foreach ($warning in @($detect.Json.warnings)) {
                if ($null -ne $warning) {
                    $warnings.Add([string]$warning) | Out-Null
                }
            }

            if ($null -ne $detect.Json.uncompressedSize) {
                $logicalSizeBytes = [int64]$detect.Json.uncompressedSize
            }
            else {
                $logicalSizeBytes = [int64]$file.Length
            }

            if ($format -eq "RawIso") {
                $sourceSha256 = Get-Sha256Hex -Path $file.FullName
            }
            elseif ($format -in @("Cso1", "Cso2", "Zso", "Dax")) {
                if ($profile -ne "game-safe") {
                    $caseSkipped = $true
                    $skippedReason = "repair supports game-safe only for CSO/ZSO/DAX/CSO2 inputs; use ISO input for profile comparison."
                    $stage = "skipped"
                    throw [System.OperationCanceledException]::new($skippedReason)
                }

                $stage = "input-verify"
                $inputVerify = Invoke-JsonTool -Arguments @("verify", $file.FullName, "--deep", "--sha256", "--json")
                $inputVerifyMilliseconds = $inputVerify.Process.ElapsedMilliseconds
                $peakWorkingSetBytes = [Math]::Max($peakWorkingSetBytes, [int64]$inputVerify.Process.PeakWorkingSetBytes)

                if (-not [bool]$inputVerify.Json.success) {
                    throw "Input deep verification failed."
                }

                $sourceSha256 = [string]$inputVerify.Json.deep.sha256

                if ($null -ne $inputVerify.Json.deep.bytesReconstructed) {
                    $logicalSizeBytes = [int64]$inputVerify.Json.deep.bytesReconstructed
                }
            }
            else {
                throw "Unsupported or unknown input format: $format"
            }

            $outputCso = Join-Path $caseDir "output.cso"
            $restoredIso = Join-Path $caseDir "restored.iso"
            $encodeArgs = @()

            if ($format -eq "RawIso") {
                $encodeArgs = @(
                    "compress",
                    $file.FullName,
                    "-o",
                    $outputCso,
                    "--profile",
                    $profile,
                    "--threads",
                    ([string]$Threads),
                    "--deep-verify",
                    "--codec-report",
                    "--codec-report-block-limit",
                    "0",
                    "--json"
                )
            }
            else {
                $encodeArgs = @(
                    "repair",
                    $file.FullName,
                    "-o",
                    $outputCso,
                    "--profile",
                    $profile,
                    "--deep-verify",
                    "--codec-report",
                    "--codec-report-block-limit",
                    "0",
                    "--json"
                )
            }

            if ($UseZopfli -and $format -eq "RawIso") {
                $encodeArgs += "--zopfli"
            }

            $stage = if ($format -eq "RawIso") { "compress" } else { "repair" }
            $encode = Invoke-JsonTool -Arguments $encodeArgs
            $encodeMilliseconds = $encode.Process.ElapsedMilliseconds
            $peakWorkingSetBytes = [Math]::Max($peakWorkingSetBytes, [int64]$encode.Process.PeakWorkingSetBytes)

            if (-not [bool]$encode.Json.success) {
                throw "Encode or normalization command reported success=false."
            }

            if (-not (Test-Path -LiteralPath $outputCso -PathType Leaf)) {
                throw "Encode command succeeded but output CSO was not found."
            }

            $outputSizeBytes = [int64](Get-Item -LiteralPath $outputCso).Length

            if ($logicalSizeBytes -gt 0) {
                $savedPercent = [Math]::Round((1.0 - ([double]$outputSizeBytes / [double]$logicalSizeBytes)) * 100.0, 4)
            }

            if ($null -ne $encode.Json.metrics) {
                if ($null -ne $encode.Json.metrics.compressedBlocks) {
                    $compressedBlocks = [int]$encode.Json.metrics.compressedBlocks
                }

                if ($null -ne $encode.Json.metrics.storedBlocks) {
                    $storedBlocks = [int]$encode.Json.metrics.storedBlocks
                }

                if ($null -ne $encode.Json.metrics.zeroBlocks) {
                    $zeroBlocks = [int]$encode.Json.metrics.zeroBlocks
                }
            }

            if ($null -ne $encode.Json.codecReport) {
                $selectedCodecWins = ConvertTo-StringIntMap -Value $encode.Json.codecReport.selectedCodecWins
                $rejectedReasons = ConvertTo-StringIntMap -Value $encode.Json.codecReport.rejectedReasons
                $candidateAttempts = ConvertTo-StringIntMap -Value $encode.Json.codecReport.candidateAttempts
                $codecCandidates = @($candidateAttempts.Keys)
                $selectedCodec = Get-TopCodec -CodecWins $selectedCodecWins
            }

            $stage = "output-verify"
            $verify = Invoke-JsonTool -Arguments @("verify", $outputCso, "--deep", "--sha256", "--json")
            $verifyMilliseconds = $verify.Process.ElapsedMilliseconds
            $peakWorkingSetBytes = [Math]::Max($peakWorkingSetBytes, [int64]$verify.Process.PeakWorkingSetBytes)
            $deepVerifySuccess = [bool]$verify.Json.success
            $outputSha256 = [string]$verify.Json.deep.sha256

            if (-not $deepVerifySuccess) {
                throw "Output deep verification failed."
            }

            $stage = "decompress"
            $decode = Invoke-JsonTool -Arguments @("decompress", $outputCso, "-o", $restoredIso, "--force", "--json")
            $decodeMilliseconds = $decode.Process.ElapsedMilliseconds
            $peakWorkingSetBytes = [Math]::Max($peakWorkingSetBytes, [int64]$decode.Process.PeakWorkingSetBytes)

            if (-not (Test-Path -LiteralPath $restoredIso -PathType Leaf)) {
                throw "Decode command succeeded but restored ISO was not found."
            }

            $stage = "hash-compare"
            $restoredSha256 = Get-Sha256Hex -Path $restoredIso
            $sha256Match = (-not [string]::IsNullOrWhiteSpace([string]$sourceSha256)) -and
                ([string]$sourceSha256 -ieq [string]$restoredSha256) -and
                ([string]$sourceSha256 -ieq [string]$outputSha256)

            if (-not $sha256Match) {
                throw "Logical SHA256 mismatch after verify/decode."
            }

            $caseSuccess = $true
        }
        catch [System.OperationCanceledException] {
            if (-not $caseSkipped) {
                $overallSuccess = $false
                $failures.Add((New-BenchmarkStageFailure -Stage $stage -Code "OperationCanceled" -Message $_.Exception.Message)) | Out-Null
            }
        }
        catch {
            $overallSuccess = $false
            $message = $_.Exception.Message
            $code = "BenchmarkStageFailed"

            if ($message -match '^([^:]+):\s*(.*)$') {
                $code = $Matches[1]
                $message = $Matches[2]
            }

            $failures.Add((New-BenchmarkStageFailure -Stage $stage -Code $code -Message $message)) | Out-Null
        }

        $resultObject = [pscustomobject]@{
            input = [string]$file.FullName
            profile = [string]$profile
            format = if ($null -eq $format) { $null } else { [string]$format }
            stage = [string]$stage
            skipped = [bool]$caseSkipped
            skippedReason = if ($null -eq $skippedReason) { $null } else { [string]$skippedReason }
            inputSizeBytes = [int64]$file.Length
            logicalSizeBytes = [int64]$logicalSizeBytes
            outputSizeBytes = [int64]$outputSizeBytes
            savedPercent = [double]$savedPercent
            encodeMilliseconds = [double]$encodeMilliseconds
            decodeMilliseconds = [double]$decodeMilliseconds
            verifyMilliseconds = [double]$verifyMilliseconds
            inputVerifyMilliseconds = [double]$inputVerifyMilliseconds
            peakWorkingSetBytes = [int64]$peakWorkingSetBytes
            compressedBlocks = [int]$compressedBlocks
            storedBlocks = [int]$storedBlocks
            zeroBlocks = [int]$zeroBlocks
            codecCandidates = [string[]]@($codecCandidates | ForEach-Object { [string]$_ })
            selectedCodec = if ($null -eq $selectedCodec) { $null } else { [string]$selectedCodec }
            selectedCodecWins = $selectedCodecWins
            rejectedReasons = $rejectedReasons
            sha256Match = [bool]$sha256Match
            deepVerifySuccess = [bool]$deepVerifySuccess
            warnings = [string[]]@($warnings.ToArray() | ForEach-Object { [string]$_ })
            failures = [object[]]@($failures.ToArray())
            success = [bool]$caseSuccess
        }

        $results.Add($resultObject) | Out-Null

        if (-not $Quiet) {
            $caseStatus = if ($caseSkipped) { "SKIP" } elseif ($caseSuccess) { "PASS" } else { "FAIL" }
            Write-Host ("[benchmark] {0} [{1}] => {2}" -f $file.Name, $profile, $caseStatus)
        }
    }
}

$caseArray = [object[]]@($results.ToArray())
$passedCases = @($caseArray | Where-Object { $_.success }).Count
$skippedCases = @($caseArray | Where-Object { $_.skipped }).Count
$failedCases = @($caseArray | Where-Object { -not $_.success -and -not $_.skipped }).Count

$artifactReportPath = $null

if ($KeepArtifacts) {
    $artifactReportPath = (Resolve-Path -LiteralPath $artifactRoot).ProviderPath
}

$report = [pscustomobject]@{
    schemaVersion = 1
    command = "benchmark-truth-layer"
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("O")
    repo = [string]$script:RepoRoot
    configuration = [string]$Configuration
    profiles = [string[]]@($Profiles | ForEach-Object { [string]$_ })
    threads = [int]$Threads
    useZopfli = [bool]$UseZopfli
    success = [bool]$overallSuccess
    skipped = [bool]($files.Count -eq 0)
    filesChecked = [int]$files.Count
    casesPassed = [int]$passedCases
    casesSkipped = [int]$skippedCases
    casesFailed = [int]$failedCases
    reportDirectory = [string](Resolve-Path -LiteralPath $OutputDirectory).ProviderPath
    artifacts = if ($null -eq $artifactReportPath) { $null } else { [string]$artifactReportPath }
    evidence = [pscustomobject]@{
        outputTarget = "CSO1"
        externalComparisonDependency = "none"
        corpusStoredInGit = $false
        failedCasesProduceTrustedOutput = $false
    }
    cases = [object[]]$caseArray
}

$jsonPath = Join-Path $OutputDirectory "benchmark-truth-report.json"
$markdownPath = Join-Path $OutputDirectory "benchmark-truth-report.md"

Write-Utf8NoBom -Path $jsonPath -Text ($report | ConvertTo-Json -Depth 18)
Write-Utf8NoBom -Path $markdownPath -Text (Get-ReportMarkdown -Report $report)

if (-not $KeepArtifacts) {
    Remove-Item -LiteralPath $artifactRoot -Recurse -Force -ErrorAction SilentlyContinue
}

if (-not $Quiet) {
    Write-Host "Benchmark truth JSON: $jsonPath"
    Write-Host "Benchmark truth Markdown: $markdownPath"
    Write-Host "Files checked: $($files.Count)"
    Write-Host "Cases passed: $passedCases"
    Write-Host "Cases skipped: $skippedCases"
    Write-Host "Cases failed: $failedCases"
    Write-Host "Status: $(if ($overallSuccess) { 'PASS' } else { 'FAILED' })"
}

if ($overallSuccess) {
    exit 0
}

exit 1
