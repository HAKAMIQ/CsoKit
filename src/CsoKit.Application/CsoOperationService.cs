using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using CsoKit.Core.Compression.Trials;
using CsoKit.Core.Formats.Containers;
using CsoKit.Core.Formats.Cso;
using CsoKit.Core.Formats.DiscImage;
using CsoKit.Core.Formats.Iso;

namespace CsoKit.Application;

public static partial class CsoOperationService
{
    private static readonly CultureInfo ReportCulture = CultureInfo.InvariantCulture;

    public static CsoOperationResult Detect(
        string inputPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        FormatDetectionResult result = FormatDetector.Detect(inputPath);
        CsoOperationDetailsBuilder details = new();

        details.Field("Input", $"{SafeFullPath(inputPath)}");

        if (result.Success)
        {
            details.Field("Format", $"{result.Format}");
            details.Field("Magic", $"{ValueOrDash(result.Magic)}");
            details.Field("Header size", $"{ValueOrDash(result.HeaderSize)}");
            details.Field("Uncompressed size", $"{ValueOrDash(result.UncompressedSize)}");
            details.Field("Block size", $"{ValueOrDash(result.BlockSize)}");
            details.Field("Index shift", $"{ValueOrDash(result.IndexShift)}");
            details.Field("Sector count", $"{ValueOrDash(result.SectorCount)}");
            AppendWarnings(details, result.Warnings);

            return CsoOperationResult.Ok(
                "Detect completed",
                details.Build(),
                inputPath: SafeFullPath(inputPath),
                format: result.Format.ToString(),
                originalBytes: result.UncompressedSize is null ? null : ToNullableLong(result.UncompressedSize.Value));
        }

        AppendError(details, result.ErrorCode, result.ErrorMessage);
        return CsoOperationResult.Fail(
            "Detect failed",
            details.Build(),
            errorCode: result.ErrorCode,
            inputPath: SafeFullPath(inputPath));
    }

    public static CsoOperationResult Analyze(
        string inputPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PspIsoValidationResult result = PspIsoValidator.Validate(inputPath, allowPadding: false);
        CsoOperationDetailsBuilder details = new();

        details.Field("Input", $"{SafeFullPath(inputPath)}");
        details.Field("Bytes", $"{result.InputBytes.ToString("N0", CultureInfo.CurrentCulture)}");
        details.Field("ISO9660 PVD", $"{result.HasIso9660PrimaryVolumeDescriptor}");
        details.Field("PSP_GAME", $"{result.HasPspGame}");
        details.Field("UMD_DATA.BIN", $"{result.HasUmdDataBin}");
        details.Field("PARAM.SFO", $"{result.HasParamSfo}");
        details.Field("EBOOT.BIN", $"{result.HasEbootBin}");

        if (!string.IsNullOrWhiteSpace(result.Title))
        {
            details.Field("Title", $"{result.Title}");
        }

        if (!string.IsNullOrWhiteSpace(result.DiscIdFromUmdData))
        {
            details.Field("DISC_ID UMD_DATA", $"{result.DiscIdFromUmdData}");
        }

        if (!string.IsNullOrWhiteSpace(result.DiscIdFromParamSfo))
        {
            details.Field("DISC_ID PARAM.SFO", $"{result.DiscIdFromParamSfo}");
        }

        AppendWarnings(details, result.Warnings);

        if (result.Issues.Count > 0)
        {
            details.Blank();
            details.Section("Issues");

            foreach (PspIsoValidationIssue issue in result.Issues)
            {
                details.Bullet($"{issue.Code}: {issue.Message}");
            }
        }

        return result.Success
            ? CsoOperationResult.Ok(
                "Analyze completed",
                details.Build(),
                inputPath: SafeFullPath(inputPath),
                format: "RawIso",
                originalBytes: result.InputBytes)
            : CsoOperationResult.Fail(
                "Analyze failed",
                details.Build(),
                errorCode: result.Issues.Count > 0 ? result.Issues[0].Code : "AnalyzeFailed",
                inputPath: SafeFullPath(inputPath),
                format: "RawIso",
                originalBytes: result.InputBytes);
    }

    public static CsoOperationResult Measure(
        string inputPath,
        CsoCompressionProfile profile,
        uint blockSize,
        IProgress<double>? progress,
        CancellationToken cancellationToken = default)
    {
        return Measure(inputPath, profile, blockSize, useZopfli: false, progress: progress, cancellationToken: cancellationToken);
    }

    public static CsoOperationResult Measure(
        string inputPath,
        CsoCompressionProfile profile,
        uint blockSize,
        bool useZopfli,
        IProgress<double>? progress,
        CancellationToken cancellationToken = default)
    {
        CsoMeasureResult result = new CsoMeasureEstimator().Measure(
            new CsoMeasureOptions(
                InputPath: inputPath,
                BlockSize: blockSize,
                Progress: CreateCompressProgress(progress),
                Profile: profile,
                UseZopfli: useZopfli,
                CancellationToken: cancellationToken));

        CsoOperationDetailsBuilder details = new();
        details.Field("Input", $"{SafeFullPath(inputPath)}");
        details.Field("Profile", $"{CsoCompressionProfilePolicy.GetCliName(profile)}");
        details.Field("Block size", $"{blockSize.ToString("N0", CultureInfo.CurrentCulture)}");
        details.Field("Original bytes", $"{result.OriginalBytes.ToString("N0", CultureInfo.CurrentCulture)}");
        details.Field("Estimated bytes", $"{result.EstimatedBytes.ToString("N0", CultureInfo.CurrentCulture)}");
        details.Field("Estimated ratio", $"{result.EstimatedRatio:P2}");
        details.Field("Compressed blocks", $"{result.CompressedBlocks.ToString("N0", CultureInfo.CurrentCulture)}");
        details.Field("Stored blocks", $"{result.StoredBlocks.ToString("N0", CultureInfo.CurrentCulture)}");

        if (!result.Success)
        {
            AppendError(details, result.ErrorCode, result.ErrorMessage);
        }

        return result.Success
            ? CsoOperationResult.Ok(
                "Measure completed",
                details.Build(),
                inputPath: SafeFullPath(inputPath),
                originalBytes: ToNullableLong(result.OriginalBytes),
                resultBytes: ToNullableLong(result.EstimatedBytes),
                data: new CsoMeasureOperationData(
                    result.OriginalBytes,
                    result.EstimatedBytes,
                    result.EstimatedSavedBytes,
                    result.EstimatedGrowthBytes,
                    result.EstimatedRatio,
                    result.TotalBlocks,
                    result.CompressedBlocks,
                    result.StoredBlocks))
            : CsoOperationResult.Fail(
                "Measure failed",
                details.Build(),
                errorCode: result.ErrorCode,
                inputPath: SafeFullPath(inputPath),
                originalBytes: ToNullableLong(result.OriginalBytes),
                resultBytes: ToNullableLong(result.EstimatedBytes),
                data: new CsoMeasureOperationData(
                    result.OriginalBytes,
                    result.EstimatedBytes,
                    result.EstimatedSavedBytes,
                    result.EstimatedGrowthBytes,
                    result.EstimatedRatio,
                    result.TotalBlocks,
                    result.CompressedBlocks,
                    result.StoredBlocks));
    }

    public static CsoOperationResult Compress(
        string inputPath,
        string outputPath,
        CsoCompressionProfile profile,
        uint blockSize,
        int workerCount,
        bool forceOverwrite,
        bool deepVerifyOutput,
        bool collectCodecReport,
        IProgress<double>? progress,
        CancellationToken cancellationToken = default)
    {
        return Compress(
            inputPath,
            outputPath,
            profile,
            blockSize,
            workerCount,
            forceOverwrite,
            deepVerifyOutput,
            collectCodecReport,
            useZopfli: false,
            codecReportBlockLimit: 64,
            progress: progress,
            cancellationToken: cancellationToken);
    }

    public static CsoOperationResult Compress(
        string inputPath,
        string outputPath,
        CsoCompressionProfile profile,
        uint blockSize,
        int workerCount,
        bool forceOverwrite,
        bool deepVerifyOutput,
        bool collectCodecReport,
        bool useZopfli,
        int codecReportBlockLimit,
        IProgress<double>? progress,
        CancellationToken cancellationToken = default)
    {
        CsoOperationResult? fileNameFailure = ValidateOutputFileName(
            inputPath,
            outputPath,
            "Compress failed");

        if (fileNameFailure is not null)
        {
            return fileNameFailure;
        }

        CsoCompressResult result = new CsoCompressor().Compress(
            new CsoCompressOptions(
                InputPath: inputPath,
                OutputPath: outputPath,
                ForceOverwrite: forceOverwrite,
                BlockSize: blockSize,
                Progress: CreateCompressProgress(progress),
                Profile: profile,
                WorkerCount: workerCount,
                UseZopfli: useZopfli,
                DeepVerifyOutput: deepVerifyOutput,
                CollectCodecReport: collectCodecReport,
                CodecReportBlockLimit: codecReportBlockLimit,
                CancellationToken: cancellationToken));

        CsoOperationDetailsBuilder details = new();
        details.Field("Input", $"{SafeFullPath(inputPath)}");
        details.Field("Output", $"{SafeFullPath(outputPath)}");
        details.Field("Profile", $"{CsoCompressionProfilePolicy.GetCliName(profile)}");
        details.Field("Block size", $"{blockSize.ToString("N0", CultureInfo.CurrentCulture)}");
        details.Field("Threads", $"{workerCount.ToString("N0", CultureInfo.CurrentCulture)}");
        details.Field("Bytes read", $"{result.BytesRead.ToString("N0", CultureInfo.CurrentCulture)}");
        details.Field("Bytes written", $"{result.BytesWritten.ToString("N0", CultureInfo.CurrentCulture)}");
        details.Field("Compressed blocks", $"{result.CompressedBlocks.ToString("N0", CultureInfo.CurrentCulture)}");
        details.Field("Stored blocks", $"{result.StoredBlocks.ToString("N0", CultureInfo.CurrentCulture)}");
        details.Field("Zero-content blocks", $"{result.ZeroBlocks.ToString("N0", ReportCulture)}");
        AppendCodecWins(details, result.EffectiveCodecWins);
        AppendCodecReport(details, result.CodecTrialSummary is null ? 0 : result.CodecTrialSummary.BlocksReported);

        if (!result.Success)
        {
            AppendError(details, result.ErrorCode, result.ErrorMessage);
        }

        return result.Success
            ? CsoOperationResult.Ok(
                "Compress completed",
                details.Build(),
                inputPath: SafeFullPath(inputPath),
                outputPath: SafeFullPath(outputPath),
                format: "Cso1",
                originalBytes: ToNullableLong(result.BytesRead),
                resultBytes: ToNullableLong(result.BytesWritten),
                data: new CsoCompressOperationData(
                    result.BytesRead,
                    result.BytesWritten,
                    result.CompressedBlocks,
                    result.StoredBlocks,
                    result.ZeroBlocks,
                    new Dictionary<string, int>(result.EffectiveCodecWins, StringComparer.OrdinalIgnoreCase),
                    MapCodecTrialSummary(result.CodecTrialSummary)))
            : CsoOperationResult.Fail(
                "Compress failed",
                details.Build(),
                errorCode: result.ErrorCode,
                inputPath: SafeFullPath(inputPath),
                outputPath: SafeFullPath(outputPath),
                originalBytes: ToNullableLong(result.BytesRead),
                resultBytes: ToNullableLong(result.BytesWritten),
                data: new CsoCompressOperationData(
                    result.BytesRead,
                    result.BytesWritten,
                    result.CompressedBlocks,
                    result.StoredBlocks,
                    result.ZeroBlocks,
                    new Dictionary<string, int>(result.EffectiveCodecWins, StringComparer.OrdinalIgnoreCase),
                    MapCodecTrialSummary(result.CodecTrialSummary)));
    }

    public static CsoOperationResult Decompress(
        string inputPath,
        string outputPath,
        bool forceOverwrite,
        IProgress<double>? progress,
        CancellationToken cancellationToken = default)
    {
        CsoOperationResult? fileNameFailure = ValidateOutputFileName(
            inputPath,
            outputPath,
            "Decompress failed");

        if (fileNameFailure is not null)
        {
            return fileNameFailure;
        }

        FormatDetectionResult detected = FormatDetector.Detect(inputPath);
        string? inputFormat = detected.Success ? detected.Format.ToString() : null;

        CsoDecompressResult result = new CsoDecompressor().Decompress(
            new CsoDecompressOptions(
                InputPath: inputPath,
                OutputPath: outputPath,
                ForceOverwrite: forceOverwrite,
                Progress: CreateDecompressProgress(progress),
                CancellationToken: cancellationToken));

        CsoOperationDetailsBuilder details = new();
        details.Field("Input", $"{SafeFullPath(inputPath)}");
        details.Field("Output", $"{SafeFullPath(outputPath)}");
        details.Field("Bytes written", $"{result.BytesWritten.ToString("N0", CultureInfo.CurrentCulture)}");

        if (!result.Success)
        {
            AppendError(details, result.ErrorCode, result.ErrorMessage);
        }

        long? inputBytes = GetFileLengthOrNull(inputPath);

        return result.Success
            ? CsoOperationResult.Ok(
                "Decompress completed",
                details.Build(),
                inputPath: SafeFullPath(inputPath),
                outputPath: SafeFullPath(outputPath),
                format: "RawIso",
                originalBytes: inputBytes,
                resultBytes: ToNullableLong(result.BytesWritten),
                data: new CsoDecompressOperationData(result.BytesWritten, inputFormat))
            : CsoOperationResult.Fail(
                "Decompress failed",
                details.Build(),
                errorCode: result.ErrorCode,
                inputPath: SafeFullPath(inputPath),
                outputPath: SafeFullPath(outputPath),
                originalBytes: inputBytes,
                resultBytes: ToNullableLong(result.BytesWritten),
                data: new CsoDecompressOperationData(result.BytesWritten, inputFormat));
    }

    public static CsoOperationResult Verify(
        string inputPath,
        bool deepVerify,
        bool computeSha256,
        IProgress<double>? progress,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CsoOperationResult result = deepVerify
            ? VerifyDeep(inputPath, computeSha256, cancellationToken)
            : VerifyShallow(inputPath);

        progress?.Report(100);
        return result;
    }

    public static CsoOperationResult Repair(
        string inputPath,
        string outputPath,
        CsoCompressionProfile profile,
        bool forceOverwrite,
        bool deepVerify,
        bool collectCodecReport,
        IProgress<double>? progress,
        CancellationToken cancellationToken = default)
    {
        return Repair(
            inputPath,
            outputPath,
            profile,
            forceOverwrite,
            padLastSector: false,
            deepVerify: deepVerify,
            collectCodecReport: collectCodecReport,
            codecReportBlockLimit: 64,
            progress: progress,
            cancellationToken: cancellationToken);
    }

    public static CsoOperationResult Repair(
        string inputPath,
        string outputPath,
        CsoCompressionProfile profile,
        bool forceOverwrite,
        bool padLastSector,
        bool deepVerify,
        bool collectCodecReport,
        int codecReportBlockLimit,
        IProgress<double>? progress,
        CancellationToken cancellationToken = default)
    {
        CsoOperationResult? fileNameFailure = ValidateOutputFileName(
            inputPath,
            outputPath,
            "Repair failed");

        if (fileNameFailure is not null)
        {
            return fileNameFailure;
        }

        CsoRepairResult result = CsoRepairer.Repair(
            new CsoRepairOptions(
                InputPath: inputPath,
                OutputPath: outputPath,
                ForceOverwrite: forceOverwrite,
                Profile: profile,
                PadLastSector: padLastSector,
                DeepVerify: deepVerify,
                CollectCodecReport: collectCodecReport,
                CodecReportBlockLimit: codecReportBlockLimit,
                Progress: CreateCompressProgress(progress),
                CancellationToken: cancellationToken));

        CsoOperationDetailsBuilder details = new();
        details.Field("Input", $"{SafeFullPath(inputPath)}");
        details.Field("Output", $"{SafeFullPath(outputPath)}");
        details.Field("Input format", $"{result.InputFormat}");
        details.Field("Profile", $"{CsoCompressionProfilePolicy.GetCliName(profile)}");
        details.Field("Repair mode", $"{result.RepairMode}");
        details.Field("Corruption detected", $"{result.CorruptionDetected}");
        details.Field("Input verification", $"{result.InputVerificationStatus}");
        details.Field("Output verification", $"{result.OutputVerificationStatus}");
        details.Field("Action taken", $"{result.ActionTaken}");
        details.Field("Conclusion", $"{result.Conclusion}");
        details.Field("Bytes read", $"{result.BytesRead.ToString("N0", CultureInfo.CurrentCulture)}");
        details.Field("Bytes written", $"{result.BytesWritten.ToString("N0", CultureInfo.CurrentCulture)}");
        details.Field("Padding bytes", $"{result.PaddingBytes.ToString("N0", CultureInfo.CurrentCulture)}");
        details.Field("Compressed blocks", $"{result.CompressedBlocks.ToString("N0", CultureInfo.CurrentCulture)}");
        details.Field("Stored blocks", $"{result.StoredBlocks.ToString("N0", CultureInfo.CurrentCulture)}");
        details.Field("Zero-content blocks", $"{result.ZeroBlocks.ToString("N0", ReportCulture)}");
        AppendRepairIssues(details, "Input verification issues", result.EffectiveInputIssues);
        AppendRepairIssues(details, "Output verification issues", result.EffectiveOutputIssues);
        AppendCodecReport(details, result.CodecTrialSummary is null ? 0 : result.CodecTrialSummary.BlocksReported);

        if (!result.Success)
        {
            AppendError(details, result.ErrorCode, result.ErrorMessage);
        }

        return result.Success
            ? CsoOperationResult.Ok(
                CreateRepairSuccessStatus(result),
                details.Build(),
                inputPath: SafeFullPath(inputPath),
                outputPath: SafeFullPath(outputPath),
                format: result.InputFormat,
                originalBytes: ToNullableLong(result.BytesRead),
                resultBytes: ToNullableLong(result.BytesWritten),
                data: new CsoRepairOperationData(
                    result.InputFormat,
                    result.BytesRead,
                    result.BytesWritten,
                    result.PaddingBytes,
                    result.Mode,
                    result.UsedTempIso,
                    result.FallbackReason,
                    result.CompressedBlocks,
                    result.StoredBlocks,
                    result.ZeroBlocks,
                    result.RepairMode.ToString(),
                    result.CorruptionDetected,
                    result.InputVerificationStatus,
                    result.OutputVerificationStatus,
                    result.ActionTaken,
                    result.Conclusion,
                    MapIssues(result.EffectiveInputIssues),
                    MapIssues(result.EffectiveOutputIssues),
                    MapCodecTrialSummary(result.CodecTrialSummary)))
            : CsoOperationResult.Fail(
                CreateRepairFailureStatus(result),
                details.Build(),
                errorCode: result.ErrorCode,
                inputPath: SafeFullPath(inputPath),
                outputPath: SafeFullPath(outputPath),
                format: result.InputFormat,
                originalBytes: ToNullableLong(result.BytesRead),
                resultBytes: ToNullableLong(result.BytesWritten),
                data: new CsoRepairOperationData(
                    result.InputFormat,
                    result.BytesRead,
                    result.BytesWritten,
                    result.PaddingBytes,
                    result.Mode,
                    result.UsedTempIso,
                    result.FallbackReason,
                    result.CompressedBlocks,
                    result.StoredBlocks,
                    result.ZeroBlocks,
                    result.RepairMode.ToString(),
                    result.CorruptionDetected,
                    result.InputVerificationStatus,
                    result.OutputVerificationStatus,
                    result.ActionTaken,
                    result.Conclusion,
                    MapIssues(result.EffectiveInputIssues),
                    MapIssues(result.EffectiveOutputIssues),
                    MapCodecTrialSummary(result.CodecTrialSummary)));
    }

    public static bool TryValidateOutputFileName(
        string outputPath,
        out string? errorMessage)
    {
        bool valid = CsoFileNamePolicy.TryValidateOutputPath(
            outputPath,
            out _,
            out errorMessage);

        return valid;
    }

    private static CsoOperationResult? ValidateOutputFileName(
        string inputPath,
        string outputPath,
        string failureStatus)
    {
        if (CsoFileNamePolicy.TryValidateOutputPath(
                outputPath,
                out string? errorCode,
                out string? errorMessage))
        {
            return null;
        }

        CsoOperationDetailsBuilder details = new();
        details.Field("Input", SafeFullPath(inputPath));
        details.Field("Output", SafeFullPath(outputPath));
        AppendError(details, errorCode, errorMessage);

        return CsoOperationResult.Fail(
            failureStatus,
            details.Build(),
            errorCode: errorCode,
            inputPath: SafeFullPath(inputPath),
            outputPath: SafeFullPath(outputPath));
    }

    public static string CreateSuggestedCompressOutputPath(string inputPath)
    {
        return new CsoOutputPathPolicy().CreateCompressionOutputPath(inputPath);
    }

    public static string CreateSuggestedDecompressOutputPath(string inputPath)
    {
        return new CsoOutputPathPolicy().CreateDecompressionOutputPath(inputPath);
    }

    public static string CreateSuggestedRepairOutputPath(string inputPath)
    {
        string fullInputPath = Path.GetFullPath(inputPath);
        string directory = Path.GetDirectoryName(fullInputPath) ?? Directory.GetCurrentDirectory();
        string baseName = NormalizeUserVisibleBaseName(Path.GetFileNameWithoutExtension(fullInputPath));

        return CsoFileNamePolicy.CreateUniquePath(
            directory,
            baseName,
            ".cso",
            preferredSuffix: "-r");
    }

    private static string NormalizeUserVisibleBaseName(string baseName)
    {
        string normalized = baseName.Trim();

        while (normalized.EndsWith(".repaired", StringComparison.OrdinalIgnoreCase) ||
               normalized.EndsWith(" - CsoKit Repaired", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized.EndsWith(".repaired", StringComparison.OrdinalIgnoreCase)
                ? normalized[..^".repaired".Length].Trim()
                : normalized[..^" - CsoKit Repaired".Length].Trim();
        }

        return normalized;
    }

    private static Progress<CsoCompressProgress>? CreateCompressProgress(IProgress<double>? progress)
    {
        if (progress is null)
        {
            return null;
        }

        return new Progress<CsoCompressProgress>(value => progress.Report(value.Percent));
    }

    private static Progress<CsoDecompressProgress>? CreateDecompressProgress(IProgress<double>? progress)
    {
        if (progress is null)
        {
            return null;
        }

        return new Progress<CsoDecompressProgress>(value => progress.Report(value.Percent));
    }

    private static void AppendWarnings(CsoOperationDetailsBuilder details, IReadOnlyList<string> warnings)
    {
        if (warnings.Count == 0)
        {
            return;
        }

        details.Blank();
        details.Section("Warnings");

        foreach (string warning in warnings)
        {
            details.Bullet($"{warning}");
        }
    }

    private static string CreateRepairSuccessStatus(CsoRepairResult result)
    {
        return result.RepairMode switch
        {
            CsoRepairMode.RebuildOnly => "Rebuild completed; no input corruption was proven",
            CsoRepairMode.DamageRepair => "Repair completed; recoverable input issues were detected",
            _ => "Repair completed",
        };
    }

    private static string CreateRepairFailureStatus(CsoRepairResult result)
    {
        return result.RepairMode switch
        {
            CsoRepairMode.ReDumpRequired => "Repair failed; re-dump required",
            CsoRepairMode.DamageRepair => "Repair failed after detecting input issues",
            _ => "Repair failed",
        };
    }

    private static void AppendCodecWins(CsoOperationDetailsBuilder details, IReadOnlyDictionary<string, int> codecWins)
    {
        if (codecWins.Count == 0)
        {
            return;
        }

        details.Blank();
        details.Section("Codec wins");

        foreach (KeyValuePair<string, int> item in codecWins)
        {
            details.Bullet($"{item.Key}: {item.Value.ToString("N0", CultureInfo.CurrentCulture)}");
        }
    }

    private static void AppendCodecReport(CsoOperationDetailsBuilder details, int blocksReported)
    {
        if (blocksReported <= 0)
        {
            return;
        }

        details.Field("Codec report blocks", $"{blocksReported.ToString("N0", CultureInfo.CurrentCulture)}");
    }

    private static void AppendError(CsoOperationDetailsBuilder details, string? code, string? message)
    {
        details.Blank();
        details.Field("Error", $"{code ?? "UnknownError"}");
        details.Text(message ?? "Operation failed.");
    }

    private static CsoCodecTrialSummaryData? MapCodecTrialSummary(CodecTrialSummary? summary)
    {
        if (summary is null)
        {
            return null;
        }

        CsoCodecTrialReportData[] blocks = summary.Blocks
            .Select(static block => new CsoCodecTrialReportData(
                block.BlockIndex,
                block.SourceBytes,
                block.Candidates.Select(static candidate => new CsoCodecTrialCandidateData(
                    candidate.CodecName,
                    candidate.CodecFamily,
                    candidate.Level,
                    candidate.CompressedBytes,
                    candidate.Ratio,
                    candidate.EncodeMilliseconds,
                    candidate.DecodeMilliseconds,
                    candidate.PassedRoundtrip,
                    candidate.RejectedReason,
                    candidate.SelectedWinner,
                    candidate.FallbackReason)).ToArray(),
                block.SelectedCodec,
                block.StoredFallback))
            .ToArray();

        return new CsoCodecTrialSummaryData(
            summary.BlocksReported,
            blocks,
            new Dictionary<string, int>(summary.SelectedCodecWins, StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, int>(summary.RejectedReasons, StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, int>(summary.CandidateAttempts, StringComparer.OrdinalIgnoreCase));
    }

    private static CsoHeaderData? CreateHeaderData(CsoHeader? header)
    {
        return header is null
            ? null
            : new CsoHeaderData(
                header.Version,
                header.HeaderSize,
                header.EffectiveHeaderSize,
                header.UncompressedSize,
                header.BlockSize,
                header.SectorCount,
                header.IndexShift,
                header.IndexEntryCount,
                header.IndexTableSizeBytes);
    }

    private static int ToEntryCount(long? value)
    {
        if (value is null || value.Value <= 0)
        {
            return 0;
        }

        return value.Value >= int.MaxValue ? int.MaxValue : (int)value.Value;
    }

    private static IReadOnlyList<CsoOperationIssueData> MapIssues(IReadOnlyList<CsoVerificationIssue> issues)
    {
        return issues.Select(static issue => new CsoOperationIssueData(
            issue.Code,
            issue.Message,
            issue.BlockIndex,
            issue.Offset,
            issue.Expected,
            issue.Actual)).ToArray();
    }

    private static IReadOnlyList<CsoOperationIssueData> MapIssues(IReadOnlyList<CsoDeepVerifyIssue> issues)
    {
        return issues.Select(static issue => new CsoOperationIssueData(
            issue.Code,
            issue.Message,
            issue.BlockIndex,
            issue.Offset,
            issue.Expected,
            issue.Actual)).ToArray();
    }

    private static long? ToNullableLong(ulong value)
    {
        return value <= long.MaxValue ? (long)value : null;
    }

    private static long? GetFileLengthOrNull(string inputPath)
    {
        try
        {
            return new FileInfo(inputPath).Length;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
        catch (PathTooLongException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string SafeFullPath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (ArgumentException)
        {
            return path;
        }
        catch (NotSupportedException)
        {
            return path;
        }
        catch (PathTooLongException)
        {
            return path;
        }
    }

    private static string ValueOrDash<T>(T? value)
        where T : struct
    {
        return value is null
            ? "-"
            : string.Format(CultureInfo.CurrentCulture, "{0:N0}", value.Value);
    }

    private static string ValueOrDash(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "-" : value;
    }
}
