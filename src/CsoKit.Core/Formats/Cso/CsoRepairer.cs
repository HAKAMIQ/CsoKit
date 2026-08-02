using CsoKit.Core.Formats.Containers;
using CsoKit.Core.Formats.DiscImage;
using CsoKit.Core.Formats.Iso;
using CsoKit.Core.Repair;

namespace CsoKit.Core.Formats.Cso;

public static class CsoRepairer
{
    private sealed record RepairVerificationSnapshot(
        bool Success,
        string Status,
        IReadOnlyList<CsoDeepVerifyIssue> Issues);

    private const ulong RepairScratchSafetyMarginBytes = 64UL * 1024UL * 1024UL;
    private const int CopyBufferSize = 1024 * 128;
    private const int PaddingBufferSize = 2048;

    public static CsoRepairResult Repair(CsoRepairOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        try
        {
            options.CancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(options.InputPath))
            {
                return CsoRepairResult.Fail("InvalidInputPath", "Input path is empty.");
            }

            if (string.IsNullOrWhiteSpace(options.OutputPath))
            {
                return CsoRepairResult.Fail("InvalidOutputPath", "Output path is empty.");
            }

            if (options.Profile != CsoCompressionProfile.GameSafe)
            {
                return CsoRepairResult.Fail(
                    "UnsupportedRepairProfile",
                    "Repair supports game-safe profile only. Use compress or benchmark for Compat/Fast/Smallest/ArchiveSmallest profile comparisons.");
            }

            if (!options.DeepVerify)
            {
                return CsoRepairResult.Fail(
                    "DeepVerifyRequired",
                    "Repair requires deep verification for game-safe output.");
            }

            if (!File.Exists(options.InputPath))
            {
                return CsoRepairResult.Fail("InputNotFound", "Input file was not found.");
            }

            if (options.CodecReportBlockLimit < 0)
            {
                return CsoRepairResult.Fail("InvalidCodecReportBlockLimit", "Codec report block limit cannot be negative.");
            }

            FormatDetectionResult format = FormatDetector.Detect(options.InputPath);

            if (!format.Success)
            {
                return CsoRepairResult.Fail(
                    format.ErrorCode ?? "FormatDetectionFailed",
                    format.ErrorMessage ?? "Format detection failed.");
            }

            RepairVerificationSnapshot inputVerification = VerifyInputBeforeRepair(
                options.InputPath,
                format.Format,
                options.DeepVerify,
                options.CancellationToken);

            CsoRepairResult repairResult = format.Format switch
            {
                DetectedDiscFormat.Cso1 or
                    DetectedDiscFormat.Cso2 or
                    DetectedDiscFormat.Zso or
                    DetectedDiscFormat.Dax => RepairContainer(options, format.Format),
                DetectedDiscFormat.RawIso => RepairIso(options),
                _ => CsoRepairResult.Fail(
                    "RepairNotPossible",
                    "Input format is not supported for safe repair.",
                    format.Format.ToString()),
            };

            return ApplyRepairDiagnostics(repairResult, inputVerification, options.DeepVerify);
        }
        catch (OperationCanceledException)
        {
            return CsoRepairResult.Fail("OperationCanceled", "Operation was canceled.");
        }
        catch (ArgumentException ex)
        {
            return CsoRepairResult.Fail("InvalidPath", ex.Message);
        }
        catch (NotSupportedException ex)
        {
            return CsoRepairResult.Fail("InvalidPath", ex.Message);
        }
        catch (PathTooLongException ex)
        {
            return CsoRepairResult.Fail("InvalidPath", ex.Message);
        }
    }

    private static RepairVerificationSnapshot VerifyInputBeforeRepair(
        string inputPath,
        DetectedDiscFormat format,
        bool deepVerify,
        CancellationToken cancellationToken)
    {
        if (!deepVerify)
        {
            return new RepairVerificationSnapshot(true, "NotRun", []);
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            CsoDeepVerifyResult result = format switch
            {
                DetectedDiscFormat.Cso1 => new CsoDeepVerifier().Verify(inputPath, computeSha256: false, cancellationToken: cancellationToken),
                DetectedDiscFormat.Cso2 or DetectedDiscFormat.Zso or DetectedDiscFormat.Dax => VerifyContainerInput(inputPath, format, cancellationToken),
                DetectedDiscFormat.RawIso => CsoDeepVerifyResult.Ok(null, blocksChecked: 0, bytesReconstructed: 0, sha256: null),
                _ => CsoDeepVerifyResult.Fail(
                    null,
                    blocksChecked: 0,
                    bytesReconstructed: 0,
                    [new CsoDeepVerifyIssue(
                        "UnsupportedRepairVerification",
                        $"{format} is not supported by repair input verification.")]),
            };

            return new RepairVerificationSnapshot(
                result.Success,
                result.Success ? "Passed" : "Failed",
                result.Issues);
        }
        catch (BlockContainerReadException ex)
        {
            return new RepairVerificationSnapshot(
                false,
                "Failed",
                [new CsoDeepVerifyIssue(ex.Code, ex.Message, ex.BlockIndex)]);
        }
        catch (InvalidDataException ex)
        {
            return new RepairVerificationSnapshot(
                false,
                "Failed",
                [new CsoDeepVerifyIssue("InvalidData", ex.Message)]);
        }
        catch (IOException ex)
        {
            return new RepairVerificationSnapshot(
                false,
                "Failed",
                [new CsoDeepVerifyIssue("InputReadFailed", ex.Message)]);
        }
    }

    private static CsoDeepVerifyResult VerifyContainerInput(
        string inputPath,
        DetectedDiscFormat format,
        CancellationToken cancellationToken)
    {
        using IBlockContainerReader reader = BlockContainerReaderFactory.Create(inputPath, format);
        return ContainerDeepVerifier.Verify(reader, computeSha256: false, cancellationToken: cancellationToken);
    }

    private static CsoRepairResult ApplyRepairDiagnostics(
        CsoRepairResult result,
        RepairVerificationSnapshot inputVerification,
        bool deepVerifyOutput)
    {
        bool inputHasIssues = !inputVerification.Success || inputVerification.Issues.Count > 0;
        bool reDumpRequired = IsReDumpRequired(result.ErrorCode) ||
            (!result.Success && inputVerification.Issues.Any(issue => IsReDumpRequired(issue.Code)));

        CsoRepairMode repairMode = result.Success
            ? inputHasIssues ? CsoRepairMode.DamageRepair : CsoRepairMode.RebuildOnly
            : reDumpRequired ? CsoRepairMode.ReDumpRequired : inputHasIssues ? CsoRepairMode.DamageRepair : CsoRepairMode.RebuildOnly;

        bool corruptionDetected = inputHasIssues || repairMode == CsoRepairMode.ReDumpRequired;
        string outputVerificationStatus = CreateOutputVerificationStatus(result, deepVerifyOutput);
        string actionTaken = CreateActionTaken(result.Success, repairMode);
        string conclusion = CreateConclusion(result.Success, repairMode, inputHasIssues, outputVerificationStatus);

        return result.WithDiagnostics(
            repairMode,
            corruptionDetected,
            inputVerification.Status,
            outputVerificationStatus,
            actionTaken,
            conclusion,
            inputVerification.Issues,
            outputIssues: []);
    }

    private static string CreateOutputVerificationStatus(CsoRepairResult result, bool deepVerifyOutput)
    {
        if (result.Success)
        {
            return deepVerifyOutput ? "Passed" : "NotRun";
        }

        return IsOutputVerificationFailure(result.ErrorCode) ? "Failed" : "NotProduced";
    }

    private static string CreateActionTaken(bool success, CsoRepairMode repairMode)
    {
        return repairMode switch
        {
            CsoRepairMode.RebuildOnly when success => "The input was readable. A normalized CSO copy was rebuilt; no input corruption was proven.",
            CsoRepairMode.DamageRepair when success => "Recoverable input verification issues were detected. The output was rebuilt and verified after writing.",
            CsoRepairMode.ReDumpRequired => "The input contains unrecoverable data damage. No safe repaired output can be trusted; re-dump from the original source.",
            CsoRepairMode.DamageRepair => "Input verification issues were detected, but the repair path did not complete successfully.",
            _ => "Repair did not complete successfully.",
        };
    }

    private static string CreateConclusion(
        bool success,
        CsoRepairMode repairMode,
        bool inputHasIssues,
        string outputVerificationStatus)
    {
        if (repairMode == CsoRepairMode.ReDumpRequired)
        {
            return "Corruption was detected and could not be safely repaired. Re-dump is required.";
        }

        if (success && repairMode == CsoRepairMode.DamageRepair)
        {
            return outputVerificationStatus == "Passed"
                ? "Input issues were detected before repair, and the rebuilt output passed post-repair verification."
                : "Input issues were detected before repair, and a rebuilt output was produced.";
        }

        if (success)
        {
            return inputHasIssues
                ? "A rebuilt output was produced, but input verification issues were recorded. Review the issue list."
                : "No corruption was proven in the input file. The output is a rebuilt normalized copy.";
        }

        return inputHasIssues
            ? "Repair failed after input verification reported issues. Review the issue list and error code."
            : "Repair failed, but no pre-repair corruption was proven by verification.";
    }

    private static bool IsOutputVerificationFailure(string? code)
    {
        return code is "CsoDeepVerifyFailed" or "VerificationFailed" or "FinalOffsetMismatch" or "FinalIndexEntryHasFlag";
    }

    private static bool IsReDumpRequired(string? code)
    {
        return code is
            "ReDumpRequired" or
            "CorruptCompressedBlock" or
            "UnexpectedEndOfFile" or
            "IndexOffsetPastEndOfFile" or
            "FinalOffsetPastEndOfFile" or
            "StoredBlockTooSmall" or
            "InvalidCompressedBlockSize" or
            "IndexOffsetsNotMonotonic" or
            "ReconstructedSizeMismatch" or
            "InputReadFailed" or
            "DeepVerifyIoFailed";
    }

    private static CsoRepairResult RepairContainer(
        CsoRepairOptions options,
        DetectedDiscFormat format)
    {
        CsoRepairResult streamingResult = new StreamingRepairService().RepairContainer(options, format);

        if (streamingResult.Success || streamingResult.ErrorCode != "StreamingRepairUnsupported")
        {
            return streamingResult;
        }

        return RepairContainerViaTempIso(options, format);
    }

    private static CsoRepairResult RepairContainerViaTempIso(
        CsoRepairOptions options,
        DetectedDiscFormat format)
    {
        string tempIso = CreateTempPath(options.OutputPath, ".repair.iso");
        string? paddedIso = null;

        try
        {
            using IBlockContainerReader reader = BlockContainerReaderFactory.Create(options.InputPath, format);
            CsoRepairResult? diskSpaceFailure = CheckRepairScratchSpace(
                reader,
                options.OutputPath,
                format);

            if (diskSpaceFailure is not null)
            {
                return diskSpaceFailure;
            }

            DecodeContainerToIso(reader, tempIso, options.CancellationToken);

            FileInfo decodedInfo = new(tempIso);
            IsoAlignmentResult alignment = IsoAlignmentPolicy.Validate(decodedInfo.Length, options.PadLastSector);

            if (!alignment.Success)
            {
                return CsoRepairResult.Fail(
                    alignment.ErrorCode ?? "IsoAlignmentFailed",
                    alignment.ErrorMessage ?? "Decoded ISO alignment validation failed.",
                    format.ToString());
            }

            string isoPath = tempIso;

            if (alignment.PaddingBytes > 0)
            {
                paddedIso = CreateTempPath(options.OutputPath, ".padded.iso");
                CopyWithPadding(tempIso, paddedIso, alignment.PaddingBytes, options.CancellationToken);
                isoPath = paddedIso;
            }

            return CompressIsoToGameSafe(
                isoPath,
                options.OutputPath,
                options.ForceOverwrite,
                options.DeepVerify,
                options.CollectCodecReport,
                options.CodecReportBlockLimit,
                options.Progress,
                inputFormat: format.ToString(),
                paddingBytes: alignment.PaddingBytes,
                mode: "temp-iso-fallback",
                usedTempIso: true,
                cancellationToken: options.CancellationToken);
        }
        catch (BlockContainerReadException ex)
        {
            return CsoRepairResult.Fail(
                MapContainerReadError(ex),
                $"{ex.Code}: {ex.Message}",
                format.ToString());
        }
        catch (InvalidDataException ex)
        {
            return CsoRepairResult.Fail(
                "RepairNotPossible",
                ex.Message,
                format.ToString());
        }
        catch (IOException ex)
        {
            return CsoRepairResult.Fail(
                "RepairNotPossible",
                ex.Message,
                format.ToString());
        }
        finally
        {
            SafeDelete(tempIso);

            if (paddedIso is not null)
            {
                SafeDelete(paddedIso);
            }
        }
    }

    private static CsoRepairResult RepairIso(CsoRepairOptions options)
    {
        FileInfo inputInfo = new(options.InputPath);
        IsoAlignmentResult alignment = IsoAlignmentPolicy.Validate(inputInfo.Length, options.PadLastSector);

        if (!alignment.Success)
        {
            return CsoRepairResult.Fail(
                alignment.ErrorCode ?? "IsoAlignmentFailed",
                alignment.ErrorMessage ?? "ISO alignment validation failed.",
                "RawIso");
        }

        string isoPath = options.InputPath;
        string? paddedIso = null;

        try
        {
            if (alignment.PaddingBytes > 0)
            {
                CsoRepairResult? diskSpaceFailure = CheckRawIsoPaddingScratchSpace(
                    inputInfo,
                    alignment.PaddingBytes,
                    options.OutputPath);

                if (diskSpaceFailure is not null)
                {
                    return diskSpaceFailure;
                }

                paddedIso = CreateTempPath(options.OutputPath, ".padded.iso");
                CopyWithPadding(options.InputPath, paddedIso, alignment.PaddingBytes, options.CancellationToken);
                isoPath = paddedIso;
            }

            return CompressIsoToGameSafe(
                isoPath,
                options.OutputPath,
                options.ForceOverwrite,
                options.DeepVerify,
                options.CollectCodecReport,
                options.CodecReportBlockLimit,
                options.Progress,
                inputFormat: "RawIso",
                paddingBytes: alignment.PaddingBytes,
                mode: alignment.PaddingBytes > 0 ? "raw-iso-padded" : "raw-iso",
                usedTempIso: alignment.PaddingBytes > 0,
                cancellationToken: options.CancellationToken);
        }
        finally
        {
            if (paddedIso is not null)
            {
                SafeDelete(paddedIso);
            }
        }
    }

    private static CsoRepairResult CompressIsoToGameSafe(
        string isoPath,
        string outputPath,
        bool forceOverwrite,
        bool deepVerify,
        bool collectCodecReport,
        int codecReportBlockLimit,
        IProgress<CsoCompressProgress>? progress,
        string inputFormat,
        long paddingBytes,
        string mode,
        bool usedTempIso,
        CancellationToken cancellationToken)
    {
        CsoCompressResult compress = new CsoCompressor().Compress(
            new CsoCompressOptions(
                isoPath,
                outputPath,
                forceOverwrite,
                CsoCompressor.DefaultBlockSize,
                Progress: progress,
                Profile: CsoCompressionProfile.GameSafe,
                WorkerCount: Math.Max(1, Environment.ProcessorCount),
                UseZopfli: false,
                DeepVerifyOutput: deepVerify,
                CollectCodecReport: collectCodecReport,
                CodecReportBlockLimit: codecReportBlockLimit,
                CancellationToken: cancellationToken));

        if (!compress.Success)
        {
            return CsoRepairResult.Fail(
                compress.ErrorCode ?? "RepairNotPossible",
                compress.ErrorMessage ?? "Repair compression failed.",
                inputFormat);
        }

        return CsoRepairResult.Ok(
            inputFormat,
            compress.BytesRead,
            compress.BytesWritten,
            paddingBytes,
            mode: mode,
            usedTempIso: usedTempIso,
            codecTrialSummary: compress.CodecTrialSummary,
            compressedBlocks: compress.CompressedBlocks,
            storedBlocks: compress.StoredBlocks,
            zeroBlocks: compress.ZeroBlocks);
    }

    private static CsoRepairResult? CheckRepairScratchSpace(
        IBlockContainerReader reader,
        string outputPath,
        DetectedDiscFormat format)
    {
        ulong requiredBytes;

        try
        {
            requiredBytes = checked(reader.UncompressedSize + reader.UncompressedSize + RepairScratchSafetyMarginBytes);
        }
        catch (OverflowException)
        {
            return CsoRepairResult.Fail(
                "DiskSpacePreflightFailed",
                $"{format} repair scratch-space estimate overflowed. Input is too large for safe normalization.",
                format.ToString());
        }

        CsoDiskSpacePreflightResult diskSpace = new CsoDiskSpacePreflight().CheckOutputSpace(
            outputPath,
            requiredBytes);

        if (diskSpace.Success)
        {
            return null;
        }

        return CsoRepairResult.Fail(
            "DiskSpacePreflightFailed",
            diskSpace.ErrorMessage ?? "Repair scratch-space preflight failed.",
            format.ToString());
    }

    private static CsoRepairResult? CheckRawIsoPaddingScratchSpace(
        FileInfo inputInfo,
        long paddingBytes,
        string outputPath)
    {
        ulong requiredBytes;

        try
        {
            requiredBytes = checked((ulong)inputInfo.Length + (ulong)paddingBytes + RepairScratchSafetyMarginBytes);
        }
        catch (OverflowException)
        {
            return CsoRepairResult.Fail(
                "DiskSpacePreflightFailed",
                "Raw ISO padding scratch-space estimate overflowed. Input is too large for safe repair.",
                "RawIso");
        }

        CsoDiskSpacePreflightResult diskSpace = new CsoDiskSpacePreflight().CheckOutputSpace(
            outputPath,
            requiredBytes);

        if (diskSpace.Success)
        {
            return null;
        }

        return CsoRepairResult.Fail(
            "DiskSpacePreflightFailed",
            diskSpace.ErrorMessage ?? "Raw ISO padding scratch-space preflight failed.",
            "RawIso");
    }

    private static void CopyWithPadding(
        string inputPath,
        string outputPath,
        long paddingBytes,
        CancellationToken cancellationToken)
    {
        if (paddingBytes < 0)
        {
            throw new InvalidDataException("Padding byte count cannot be negative.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        using FileStream input = new(
            inputPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: CopyBufferSize,
            FileOptions.SequentialScan);

        using FileStream output = new(
            outputPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: CopyBufferSize,
            FileOptions.SequentialScan);

        byte[] copyBuffer = new byte[CopyBufferSize];

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int read = input.Read(copyBuffer);

            if (read == 0)
            {
                break;
            }

            output.Write(copyBuffer.AsSpan(0, read));
        }

        if (paddingBytes > 0)
        {
            byte[] paddingBuffer = new byte[PaddingBufferSize];
            long remaining = paddingBytes;

            while (remaining > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int writeBytes = checked((int)Math.Min(paddingBuffer.Length, remaining));
                output.Write(paddingBuffer.AsSpan(0, writeBytes));
                remaining -= writeBytes;
            }
        }

        output.Flush(true);
    }


    private static void DecodeContainerToIso(
        IBlockContainerReader reader,
        string outputIsoPath,
        CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[checked((int)reader.BlockSize)];
        ulong totalWritten = 0;

        using FileStream output = new(
            outputIsoPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: CopyBufferSize,
            FileOptions.SequentialScan);

        for (int blockIndex = 0; blockIndex < reader.BlockCount; blockIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int bytesRead = reader.ReadBlock(blockIndex, buffer);

            if (bytesRead <= 0)
            {
                throw new BlockContainerReadException(
                    "CorruptCompressedBlock",
                    $"{reader.Format} block {blockIndex} decoded to an empty payload. Re-dump required.",
                    blockIndex);
            }

            output.Write(buffer.AsSpan(0, bytesRead));
            totalWritten += (ulong)bytesRead;
        }

        if (totalWritten != reader.UncompressedSize)
        {
            throw new BlockContainerReadException(
                "RepairNotPossible",
                $"{reader.Format} decoded size mismatch. Written {totalWritten:N0} bytes, expected {reader.UncompressedSize:N0} bytes.");
        }

        output.Flush(true);
    }

    private static string MapContainerReadError(BlockContainerReadException exception)
    {
        return exception.Code switch
        {
            "UnsupportedContainer" => "UnsupportedContainer",
            "CorruptCompressedBlock" or
                "UnexpectedEndOfFile" or
                "IndexOffsetPastEndOfFile" or
                "FinalOffsetPastEndOfFile" or
                "StoredBlockTooSmall" or
                "InvalidCompressedBlockSize" or
                "IndexOffsetsNotMonotonic" => "ReDumpRequired",
            _ => exception.Code,
        };
    }

    private static string CreateTempPath(string outputPath, string suffix)
    {
        string directory = Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? ".";
        string kind = suffix.Contains("padded", StringComparison.OrdinalIgnoreCase) ? "padded" : "repair";
        string extension = suffix.EndsWith(".iso", StringComparison.OrdinalIgnoreCase) ? ".iso" : ".tmp";

        for (int attempt = 0; attempt < 16; attempt++)
        {
            string candidate = Path.Combine(directory, $".{kind}-{CreateShortId()}{extension}");

            if (!File.Exists(candidate) && !Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new IOException("Could not create a unique temporary repair path.");
    }

    private static string CreateShortId()
    {
        return Guid.NewGuid().ToString("N")[..8];
    }

    private static void SafeDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }
}