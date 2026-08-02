using System.Diagnostics;
using System.Globalization;
using CsoKit.Core.Formats.Containers;
using CsoKit.Core.Formats.Cso;
using CsoKit.Core.Formats.DiscImage;
using CsoKit.Core.Formats.Iso;

namespace CsoKit.Application;

public static partial class CsoOperationService
{
    private static CsoOperationResult VerifyShallow(string inputPath)
    {
        FormatDetectionResult detected = FormatDetector.Detect(inputPath);
        CsoOperationDetailsBuilder details = new();
        details.Field("Input", $"{SafeFullPath(inputPath)}");
        details.Field("Verification type", "Shallow");
        details.Field("Output written", "False");
        details.Field("Action taken", "Header and index metadata were inspected only; compressed block payloads were not decompressed.");

        if (!detected.Success)
        {
            details.Field("Result", "Failed");
            details.Field("Corruption detected", "Unknown");
            details.Field("Conclusion", "The input could not be identified as a supported CSO-like file. No corruption verdict was produced.");
            AppendError(details, detected.ErrorCode, detected.ErrorMessage);

            return CsoOperationResult.Fail(
                "Verify failed; input format was not recognized",
                details.Build(),
                errorCode: detected.ErrorCode,
                inputPath: SafeFullPath(inputPath),
                data: new CsoVerifyOperationData(
                    Deep: false,
                    DetectedFormat: null,
                    Header: null,
                    EntriesRead: 0,
                    ExpectedEntries: null,
                    BlocksChecked: 0,
                    BytesReconstructed: 0,
                    Sha256: null,
                    Issues: [new CsoOperationIssueData(
                        detected.ErrorCode ?? "FormatDetectionFailed",
                        detected.ErrorMessage ?? "Format detection failed.")]));
        }

        details.Field("Format", $"{detected.Format}");

        if (detected.Format is not (DetectedDiscFormat.Cso1 or DetectedDiscFormat.Cso2))
        {
            details.Field("Result", "Failed");
            details.Field("Corruption detected", "Unknown");
            details.Field("Conclusion", $"Shallow verify supports CSO1/CSO2 only. Detected format: {detected.Format}. Use Deep verify for ZSO/DAX.");

            return CsoOperationResult.Fail(
                "Verify failed; unsupported shallow format",
                details.Build(),
                errorCode: "UnsupportedContainer",
                inputPath: SafeFullPath(inputPath),
                format: detected.Format.ToString(),
                data: new CsoVerifyOperationData(
                    Deep: false,
                    DetectedFormat: detected.Format.ToString(),
                    Header: null,
                    EntriesRead: 0,
                    ExpectedEntries: null,
                    BlocksChecked: 0,
                    BytesReconstructed: 0,
                    Sha256: null,
                    Issues: [new CsoOperationIssueData(
                        "UnsupportedContainer",
                        $"Shallow verification does not support {detected.Format}.")]));
        }

        CsoVerificationResult result = new CsoVerifier().Verify(inputPath);
        details.Field("Result", $"{(result.Success ? "Passed" : "Failed")}");
        details.Field("Corruption detected", $"{FormatBoolean(!result.Success)}");
        details.Field("Repair needed", "Not determined by shallow verify");

        if (result.Header is not null)
        {
            details.Field("Version", $"{result.Header.Version}");
            details.Field("Uncompressed size", $"{result.Header.UncompressedSize.ToString("N0", CultureInfo.CurrentCulture)}");
            details.Field("Block size", $"{result.Header.BlockSize.ToString("N0", CultureInfo.CurrentCulture)}");
            details.Field("Sectors", $"{result.Header.SectorCount.ToString("N0", CultureInfo.CurrentCulture)}");
        }

        details.Field("Index entries", $"{result.Entries.Count.ToString("N0", CultureInfo.CurrentCulture)}");
        AppendVerificationIssues(details, result.Issues);
        details.Field("Conclusion", $"{CreateShallowVerifyConclusion(result)}");

        long? inputBytes = GetFileLengthOrNull(inputPath);

        return result.Success
            ? CsoOperationResult.Ok(
                "Shallow verify passed; no header/index corruption detected",
                details.Build(),
                inputPath: SafeFullPath(inputPath),
                format: detected.Format.ToString(),
                originalBytes: inputBytes,
                resultBytes: inputBytes,
                data: new CsoVerifyOperationData(
                    Deep: false,
                    DetectedFormat: detected.Format.ToString(),
                    Header: CreateHeaderData(result.Header),
                    EntriesRead: result.Entries.Count,
                    ExpectedEntries: result.Header?.IndexEntryCount,
                    BlocksChecked: 0,
                    BytesReconstructed: 0,
                    Sha256: null,
                    Issues: MapIssues(result.Issues)))
            : CsoOperationResult.Fail(
                "Shallow verify failed; structural issues detected",
                details.Build(),
                errorCode: result.Issues.Count > 0 ? result.Issues[0].Code : "VerificationFailed",
                inputPath: SafeFullPath(inputPath),
                format: detected.Format.ToString(),
                originalBytes: inputBytes,
                resultBytes: inputBytes,
                data: new CsoVerifyOperationData(
                    Deep: false,
                    DetectedFormat: detected.Format.ToString(),
                    Header: CreateHeaderData(result.Header),
                    EntriesRead: result.Entries.Count,
                    ExpectedEntries: result.Header?.IndexEntryCount,
                    BlocksChecked: 0,
                    BytesReconstructed: 0,
                    Sha256: null,
                    Issues: MapIssues(result.Issues)));
    }

    private static CsoOperationResult VerifyDeep(
        string inputPath,
        bool computeSha256,
        CancellationToken cancellationToken)
    {
        FormatDetectionResult detected = FormatDetector.Detect(inputPath);
        CsoOperationDetailsBuilder details = new();
        details.Field("Input", $"{SafeFullPath(inputPath)}");
        details.Field("Verification type", "Deep");
        details.Field("Output written", "False");
        details.Field("Action taken", "The file was read block-by-block and payload data was reconstructed in memory. No repair output was produced.");

        if (!detected.Success)
        {
            details.Field("Result", "Failed");
            details.Field("Corruption detected", "Unknown");
            details.Field("Conclusion", "The input could not be identified as a supported CSO-like file. No corruption verdict was produced.");
            AppendError(details, detected.ErrorCode, detected.ErrorMessage);

            return CsoOperationResult.Fail(
                "Deep verify failed; input format was not recognized",
                details.Build(),
                errorCode: detected.ErrorCode,
                inputPath: SafeFullPath(inputPath),
                data: new CsoVerifyOperationData(
                    Deep: true,
                    DetectedFormat: null,
                    Header: null,
                    EntriesRead: 0,
                    ExpectedEntries: null,
                    BlocksChecked: 0,
                    BytesReconstructed: 0,
                    Sha256: null,
                    Issues: [new CsoOperationIssueData(
                        detected.ErrorCode ?? "FormatDetectionFailed",
                        detected.ErrorMessage ?? "Format detection failed.")]));
        }

        details.Field("Format", $"{detected.Format}");

        Stopwatch stopwatch = Stopwatch.StartNew();
        CsoDeepVerifyResult result = detected.Format switch
        {
            DetectedDiscFormat.Cso1 => new CsoDeepVerifier().Verify(inputPath, computeSha256, cancellationToken),
            DetectedDiscFormat.RawIso or DetectedDiscFormat.Cso2 or DetectedDiscFormat.Zso or DetectedDiscFormat.Dax => RunContainerDeepVerify(inputPath, detected.Format, computeSha256, cancellationToken),
            _ => CsoDeepVerifyResult.Fail(
                null,
                blocksChecked: 0,
                bytesReconstructed: 0,
                [new CsoDeepVerifyIssue("UnsupportedContainer", $"{detected.Format} is not supported by deep verification.")]),
        };
        stopwatch.Stop();

        AppendDeepVerifyDiagnostics(details, result, stopwatch.Elapsed, detected.Format);
        AppendDeepIssues(details, result.Issues);
        details.Field("Conclusion", $"{CreateDeepVerifyConclusion(result, detected.Format)}");
        details.Field("Limitations", $"{CreateDeepVerifyLimitations(detected.Format)}");

        long? inputBytes = GetFileLengthOrNull(inputPath);

        return result.Success
            ? CsoOperationResult.Ok(
                "Deep verify passed; no corruption detected",
                details.Build(),
                inputPath: SafeFullPath(inputPath),
                format: detected.Format.ToString(),
                originalBytes: inputBytes,
                resultBytes: inputBytes,
                data: new CsoVerifyOperationData(
                    Deep: true,
                    DetectedFormat: detected.Format.ToString(),
                    Header: CreateHeaderData(result.Header),
                    EntriesRead: ToEntryCount(result.IndexEntryCount),
                    ExpectedEntries: result.Header?.IndexEntryCount ?? result.IndexEntryCount,
                    BlocksChecked: result.BlocksChecked,
                    BytesReconstructed: result.BytesReconstructed,
                    Sha256: result.Sha256,
                    Issues: MapIssues(result.Issues)))
            : CsoOperationResult.Fail(
                CreateDeepVerifyFailureStatus(result),
                details.Build(),
                errorCode: result.Issues.Count > 0 ? result.Issues[0].Code : "VerificationFailed",
                inputPath: SafeFullPath(inputPath),
                format: detected.Format.ToString(),
                originalBytes: inputBytes,
                resultBytes: inputBytes,
                data: new CsoVerifyOperationData(
                    Deep: true,
                    DetectedFormat: detected.Format.ToString(),
                    Header: CreateHeaderData(result.Header),
                    EntriesRead: ToEntryCount(result.IndexEntryCount),
                    ExpectedEntries: result.Header?.IndexEntryCount ?? result.IndexEntryCount,
                    BlocksChecked: result.BlocksChecked,
                    BytesReconstructed: result.BytesReconstructed,
                    Sha256: result.Sha256,
                    Issues: MapIssues(result.Issues)));
    }

    private static CsoDeepVerifyResult RunContainerDeepVerify(
        string inputPath,
        DetectedDiscFormat format,
        bool computeSha256,
        CancellationToken cancellationToken)
    {
        try
        {
            CsoDeepVerifyResult? earlyFailure = ValidateRawIsoBeforeDeepRead(inputPath, format);

            if (earlyFailure is not null)
            {
                return earlyFailure;
            }

            using IBlockContainerReader reader = BlockContainerReaderFactory.Create(
                inputPath,
                format,
                allowRawIso: true);
            CsoDeepVerifyResult result = ContainerDeepVerifier.Verify(reader, computeSha256, cancellationToken);
            long? fileLength = GetFileLengthOrNull(inputPath);

            return fileLength is null || result.FileLength is not null
                ? result
                : result with { FileLength = fileLength.Value };
        }
        catch (BlockContainerReadException ex)
        {
            return CsoDeepVerifyResult.Fail(
                null,
                blocksChecked: 0,
                bytesReconstructed: 0,
                [new CsoDeepVerifyIssue(ex.Code, ex.Message, ex.BlockIndex)]);
        }
        catch (UnauthorizedAccessException ex)
        {
            return CsoDeepVerifyResult.Fail(
                null,
                blocksChecked: 0,
                bytesReconstructed: 0,
                [new CsoDeepVerifyIssue("InputAccessDenied", ex.Message)]);
        }
        catch (IOException ex)
        {
            return CsoDeepVerifyResult.Fail(
                null,
                blocksChecked: 0,
                bytesReconstructed: 0,
                [new CsoDeepVerifyIssue("DeepVerifyIoFailed", ex.Message)]);
        }
    }

    private static CsoDeepVerifyResult? ValidateRawIsoBeforeDeepRead(
        string inputPath,
        DetectedDiscFormat format)
    {
        if (format is not DetectedDiscFormat.RawIso)
        {
            return null;
        }

        long? fileLength = GetFileLengthOrNull(inputPath);

        if (fileLength is null)
        {
            return null;
        }

        IsoAlignmentResult alignment = IsoAlignmentPolicy.Validate(fileLength.Value, allowPadding: false);

        if (alignment.Success)
        {
            return null;
        }

        long totalBlocks = fileLength.Value <= 0
            ? 0
            : checked((fileLength.Value + IsoAlignmentPolicy.SectorSize - 1) / IsoAlignmentPolicy.SectorSize);

        return CsoDeepVerifyResult.Fail(
            header: null,
            blocksChecked: 0,
            bytesReconstructed: 0,
            [new CsoDeepVerifyIssue(
                alignment.ErrorCode ?? "IsoAlignmentFailed",
                alignment.ErrorMessage ?? "Raw ISO alignment validation failed.")]) with
        {
            AlgorithmName = "Hybrid raw ISO verification",
            VerificationScope = "ISO9660 probe + raw sector read + full payload reconstruction",
            LegacyLayer = "ISO9660 primary-volume probe and strict 2048-byte sector-alignment validation",
            ModernLayer = "Not reached because raw ISO alignment validation failed.",
            ForensicLayer = "Coverage, zero-content, bounds, and reconstruction diagnostics",
            FileLength = fileLength.Value,
            TotalBlocks = totalBlocks,
            ExpectedReconstructedBytes = fileLength.Value > 0 ? (ulong)fileLength.Value : 0,
        };
    }


    private static void AppendDeepVerifyDiagnostics(
        CsoOperationDetailsBuilder details,
        CsoDeepVerifyResult result,
        TimeSpan elapsed,
        DetectedDiscFormat format)
    {
        details.Blank();
        details.Section("Verification layers");
        details.Field("Algorithm", $"{result.AlgorithmName}");
        details.Field("Scope", $"{result.VerificationScope}");
        details.Field("Legacy layer", $"{result.LegacyLayer}");
        details.Field("Modern layer", $"{result.ModernLayer}");
        details.Field("Forensic layer", $"{result.ForensicLayer}");

        details.Blank();
        details.Section("Integrity checks");
        details.Field("Header check", $"{CreateHeaderCheckStatus(result, format)}");
        details.Field("Index check", $"{CreateIndexCheckStatus(result, format)}");
        details.Field("Final sentinel", $"{CreateFinalSentinelStatus(result, format)}");
        details.Field("Block offset order", $"{CreateOffsetOrderStatus(result)}");
        details.Field("Bounds check", $"{CreateBoundsStatus(result)}");
        details.Field("Payload read/decode", $"{CreatePayloadStatus(result)}");
        details.Field("Reconstructed size", $"{CreateReconstructedSizeStatus(result)}");

        details.Blank();
        if (format is DetectedDiscFormat.RawIso)
        {
            details.Section("Raw image metadata");
            details.Field("Image format", $"{format}");
            details.Field("Sector size", $"{FormatByteCount((ulong)IsoAlignmentPolicy.SectorSize)}");
            details.Field("Logical image size", $"{FormatByteCount(result.ExpectedReconstructedBytes)}");
            details.Field("Physical file size", $"{FormatNullableByteCount(result.FileLength)}");
            details.Field("Container ratio", $"{FormatContainerRatio(result)}");
            details.Field("Space saved", $"{FormatSpaceSaved(result)}");
        }
        else
        {
            details.Section("CSO metadata");
            details.Field("CSO version", $"{FormatCsoVersion(result)}");
            details.Field("Block size", $"{FormatNullableByteCount(result.Header?.BlockSize)}");
            details.Field("Index shift", $"{FormatNullableNumber(result.Header?.IndexShift)}");
            details.Field("Uncompressed size", $"{FormatByteCount(result.ExpectedReconstructedBytes)}");
            details.Field("Compressed file size", $"{FormatNullableByteCount(result.FileLength)}");
            details.Field("Container ratio", $"{FormatContainerRatio(result)}");
            details.Field("Space saved", $"{FormatSpaceSaved(result)}");
        }

        details.Blank();
        details.Section("Forensic statistics");
        details.Field("Result", $"{(result.Success ? "Passed" : "Failed")}");
        details.Field("Corruption detected", $"{CreateCorruptionVerdict(result)}");
        details.Field("Coverage", $"{FormatCoverage(result)}");
        details.Field("Blocks checked", $"{FormatBlocksChecked(result)}");
        details.Field("Bytes reconstructed", $"{FormatByteCount(result.BytesReconstructed)}");
        details.Field("Expected reconstructed bytes", $"{FormatByteCount(result.ExpectedReconstructedBytes)}");
        details.Field("File length", $"{FormatNullableByteCount(result.FileLength)}");
        details.Field("Header size", $"{FormatNullableNumber(result.HeaderSize)}");
        details.Field("Index entries", $"{FormatNullableNumber(result.IndexEntryCount)}");
        details.Field("Index table bytes", $"{FormatNullableNumber(result.IndexTableBytes)}");
        details.Field("Index end offset", $"{FormatNullableNumber(result.IndexEndOffset)}");
        details.Field("First data offset", $"{FormatNullableNumber(result.FirstDataOffset)}");
        details.Field("Final data offset", $"{FormatNullableNumber(result.FinalDataOffset)}");
        details.Field("Physical payload bytes", $"{FormatByteCount(result.PhysicalPayloadBytes)}");
        details.Field("Payload blocks decoded", $"{FormatNumber(result.PayloadBlocksDecoded)}");
        details.Field("Compressed blocks", $"{FormatNumber(result.CompressedBlocks)}");
        details.Field("Stored blocks", $"{FormatNumber(result.StoredBlocks)}");
        details.Field("Decoded zero-content blocks", $"{FormatNumber(result.ZeroBlocks)}");
        details.Field("Zero-content note", "Counted after payload decode; may overlap compressed/stored block counts.");
        details.Field("Reconstructed SHA256", $"{CreateSha256Status(result)}");
        details.Field("Elapsed", $"{FormatElapsed(elapsed)}");
        details.Field("Throughput", $"{FormatThroughput(result.BytesReconstructed, elapsed)}");
        details.Field("Repair needed", $"{CreateDeepRepairNeed(result)}");
    }

    private static void AppendVerificationIssues(CsoOperationDetailsBuilder details, IReadOnlyList<CsoVerificationIssue> issues)
    {
        if (issues.Count == 0)
        {
            details.Field("Issues", "none");
            return;
        }

        details.Blank();
        details.Section("Issues");

        foreach (CsoVerificationIssue issue in issues)
        {
            string block = issue.BlockIndex is null
                ? string.Empty
                : $" [block {issue.BlockIndex.Value.ToString("N0", CultureInfo.CurrentCulture)}]";

            details.Bullet($"{issue.Code}{block}: {issue.Message}");
        }
    }

    private static void AppendDeepIssues(CsoOperationDetailsBuilder details, IReadOnlyList<CsoDeepVerifyIssue> issues)
    {
        if (issues.Count == 0)
        {
            details.Field("Issues", "none");
            return;
        }

        details.Blank();
        details.Section("Issues");

        foreach (CsoDeepVerifyIssue issue in issues)
        {
            string block = issue.BlockIndex is null
                ? string.Empty
                : $" [block {issue.BlockIndex.Value.ToString("N0", CultureInfo.CurrentCulture)}]";

            details.Bullet($"{issue.Code}{block}: {issue.Message}");
        }
    }

    private static void AppendRepairIssues(
        CsoOperationDetailsBuilder details,
        string heading,
        IReadOnlyList<CsoDeepVerifyIssue> issues)
    {
        if (issues.Count == 0)
        {
            details.Field(heading, $"none");
            return;
        }

        details.Blank();
        details.Section(heading);

        foreach (CsoDeepVerifyIssue issue in issues)
        {
            string block = issue.BlockIndex is null
                ? string.Empty
                : $" [block {issue.BlockIndex.Value.ToString("N0", CultureInfo.CurrentCulture)}]";

            details.Bullet($"{issue.Code}{block}: {issue.Message}");
        }
    }

    private static string FormatBoolean(bool value)
    {
        return value ? "True" : "False";
    }

    private static string CreateHeaderCheckStatus(CsoDeepVerifyResult result, DetectedDiscFormat format)
    {
        if (format is DetectedDiscFormat.RawIso)
        {
            return "N/A for raw image";
        }

        if (HasAnyIssue(result, "InvalidMagic", "HeaderTooSmall", "UnsupportedVersion", "InvalidHeaderSize", "InvalidUncompressedSize", "InvalidBlockSize", "BlockSizeTooLarge", "InvalidIndexShift", "HeaderReadFailed"))
        {
            return "Failed";
        }

        return result.Header is null && result.TotalBlocks > 0
            ? "Passed via container reader"
            : result.Header is null ? "Not completed" : "Passed";
    }

    private static string CreateIndexCheckStatus(CsoDeepVerifyResult result, DetectedDiscFormat format)
    {
        if (format is DetectedDiscFormat.RawIso)
        {
            return "N/A for raw image";
        }

        if (HasAnyIssue(result, "IndexReadFailed", "IndexEntryCountMismatch", "EmptyIndexTable", "IndexTableTooLarge", "IndexTableTruncated", "IndexEntryTruncated", "FirstDataOffsetBeforeIndexEnd"))
        {
            return "Failed";
        }

        return result.TotalBlocks > 0 || result.Success ? "Passed" : "Not completed";
    }

    private static string CreateFinalSentinelStatus(CsoDeepVerifyResult result, DetectedDiscFormat format)
    {
        if (format is DetectedDiscFormat.RawIso)
        {
            return "N/A for raw image";
        }

        if (HasAnyIssue(result, "FinalIndexEntryHasFlag", "CsoV2FinalSentinelHighBit", "FinalOffsetMismatch", "FinalOffsetPastEndOfFile"))
        {
            return "Failed";
        }

        return result.Header is null ? "N/A for this container reader" : "Passed";
    }

    private static string CreateOffsetOrderStatus(CsoDeepVerifyResult result)
    {
        return HasAnyIssue(result, "IndexOffsetsNotMonotonic") ? "Failed" : result.TotalBlocks > 0 ? "Passed" : "Not completed";
    }

    private static string CreateBoundsStatus(CsoDeepVerifyResult result)
    {
        return HasAnyIssue(result, "IndexOffsetPastEndOfFile", "FinalOffsetPastEndOfFile", "StoredBlockTooSmall", "InvalidCompressedBlockSize", "IsoNotSectorAligned", "InvalidIsoSize", "IsoAlignmentFailed")
            ? "Failed"
            : result.TotalBlocks > 0 ? "Passed" : "Not completed";
    }

    private static string CreatePayloadStatus(CsoDeepVerifyResult result)
    {
        if (HasAnyIssue(result, "CorruptCompressedBlock", "UnexpectedEndOfFile", "CsoDeepVerifyFailed", "DeepVerifyIoFailed", "InputReadFailed"))
        {
            return "Failed";
        }

        if (result.PayloadBlocksDecoded > 0 || result.Success)
        {
            return "Passed";
        }

        return "Not completed";
    }

    private static string CreateReconstructedSizeStatus(CsoDeepVerifyResult result)
    {
        return HasAnyIssue(result, "ReconstructedSizeMismatch") ? "Failed" : result.Success ? "Passed" : "Not completed";
    }

    private static bool HasAnyIssue(CsoDeepVerifyResult result, params string[] issueCodes)
    {
        foreach (CsoDeepVerifyIssue issue in result.Issues)
        {
            foreach (string code in issueCodes)
            {
                if (string.Equals(issue.Code, code, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string FormatCoverage(CsoDeepVerifyResult result)
    {
        return result.TotalBlocks <= 0
            ? "-"
            : $"{result.CoveragePercent.ToString("N2", ReportCulture)}% of indexed blocks";
    }

    private static string FormatBlocksChecked(CsoDeepVerifyResult result)
    {
        string checkedBlocks = FormatNumber(result.BlocksChecked);

        return result.TotalBlocks <= 0
            ? checkedBlocks
            : $"{checkedBlocks} / {FormatNumber(result.TotalBlocks)}";
    }

    private static string FormatByteCount(ulong bytes)
    {
        return bytes < 1024UL * 1024UL
            ? $"{FormatNumber(bytes)} bytes"
            : $"{FormatNumber(bytes)} bytes ({FormatMib(bytes)} MiB)";
    }

    private static string FormatNullableByteCount(long? bytes)
    {
        return bytes is null || bytes.Value < 0
            ? "-"
            : FormatByteCount((ulong)bytes.Value);
    }

    private static string FormatNullableByteCount(uint? bytes)
    {
        return bytes is null ? "-" : FormatByteCount(bytes.Value);
    }

    private static string FormatMib(ulong bytes)
    {
        return ((double)bytes / (1024d * 1024d)).ToString("N2", ReportCulture);
    }

    private static string FormatNumber(long value)
    {
        return value.ToString("N0", ReportCulture);
    }

    private static string FormatNumber(ulong value)
    {
        return value.ToString("N0", ReportCulture);
    }

    private static string FormatNullableNumber(long? value)
    {
        return value is null ? "-" : FormatNumber(value.Value);
    }

    private static string FormatNullableNumber(byte? value)
    {
        return value is null ? "-" : value.Value.ToString("N0", ReportCulture);
    }

    private static string FormatCsoVersion(CsoDeepVerifyResult result)
    {
        return result.Header is null ? "-" : result.Header.Version.ToString("N0", ReportCulture);
    }

    private static string FormatContainerRatio(CsoDeepVerifyResult result)
    {
        if (result.FileLength is null || result.FileLength <= 0 || result.ExpectedReconstructedBytes == 0)
        {
            return "-";
        }

        double ratio = (double)result.FileLength.Value / result.ExpectedReconstructedBytes;
        return ratio.ToString("P2", ReportCulture);
    }

    private static string FormatSpaceSaved(CsoDeepVerifyResult result)
    {
        if (result.FileLength is null || result.FileLength <= 0 || result.ExpectedReconstructedBytes == 0)
        {
            return "-";
        }

        double saved = 1.0 - ((double)result.FileLength.Value / result.ExpectedReconstructedBytes);
        return saved.ToString("P2", ReportCulture);
    }

    private static string CreateSha256Status(CsoDeepVerifyResult result)
    {
        return result.Sha256Computed ? result.Sha256! : "Disabled";
    }

    private static string CreateCorruptionVerdict(CsoDeepVerifyResult result)
    {
        if (result.Success)
        {
            return "False";
        }

        return HasAnyIssue(
            result,
            "InvalidInputPath",
            "InputNotFound",
            "InputAccessDenied",
            "UnsupportedContainer",
            "StreamNotSeekable",
            "DeepVerifyIoFailed")
            ? "Unknown"
            : "True";
    }

    private static string CreateDeepVerifyFailureStatus(CsoDeepVerifyResult result)
    {
        return string.Equals(CreateCorruptionVerdict(result), "Unknown", StringComparison.Ordinal)
            ? "Deep verify failed; no corruption verdict"
            : "Deep verify failed; corruption detected";
    }

    private static string FormatElapsed(TimeSpan elapsed)
    {
        return elapsed.ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture);
    }

    private static string FormatThroughput(ulong bytes, TimeSpan elapsed)
    {
        if (bytes == 0 || elapsed.TotalSeconds <= 0)
        {
            return "-";
        }

        double mibPerSecond = bytes / (1024d * 1024d) / elapsed.TotalSeconds;
        return $"{mibPerSecond.ToString("N2", ReportCulture)} MiB/s";
    }

    private static string CreateShallowVerifyConclusion(CsoVerificationResult result)
    {
        return result.Success
            ? "No header/index corruption was detected. This is a metadata-only pass and does not prove that every compressed block can be decompressed."
            : "Structural CSO metadata issues were detected. Run Deep verify or Repair to classify the damage.";
    }

    private static string CreateDeepRepairNeed(CsoDeepVerifyResult result)
    {
        return result.Success
            ? "No"
            : "Yes or re-dump required; see Issues for the exact failing block/condition.";
    }

    private static string CreateDeepVerifyConclusion(CsoDeepVerifyResult result, DetectedDiscFormat format)
    {
        if (format is DetectedDiscFormat.RawIso)
        {
            return result.Success
                ? "No raw-image read, alignment, or reconstruction problems were detected. The input was readable and every checked sector reconstructed successfully."
                : "Raw-image read, alignment, or unsupported container structure failed. The file did not fully reconstruct under deep verification.";
        }

        return result.Success
            ? "No corruption was detected by deep verification. The input was readable and all checked payload blocks reconstructed successfully."
            : "Corruption or unsupported container structure was detected. The file did not fully reconstruct under deep verification.";
    }

    private static string CreateDeepVerifyLimitations(DetectedDiscFormat format)
    {
        return format is DetectedDiscFormat.RawIso
            ? "This verification validates raw image readability, 2048-byte sector alignment, full block coverage, and payload reconstruction. It does not prove Redump hash match, game database identity, or gameplay correctness."
            : "This verification validates container structure, index/bounds semantics, and payload decompression. It does not prove Redump hash match, game database identity, or gameplay correctness.";
    }

}
