using CsoKit.Core.Formats.Containers;
using CsoKit.Core.Formats.Cso;
using CsoKit.Core.Formats.DiscImage;

namespace CsoKit.Core.Repair;

public sealed class StreamingRepairService
{
    public CsoRepairResult RepairContainer(
        CsoRepairOptions options,
        DetectedDiscFormat format)
    {
        ArgumentNullException.ThrowIfNull(options);

        try
        {
            using IBlockContainerReader reader = BlockContainerReaderFactory.Create(options.InputPath, format);
            CsoCompressResult write = new Cso1Writer().WriteFromContainer(
                options.InputPath,
                reader,
                options.OutputPath,
                options.ForceOverwrite,
                options.Profile,
                options.DeepVerify || options.Profile == CsoCompressionProfile.GameSafe,
                options.CollectCodecReport,
                options.CodecReportBlockLimit,
                options.Progress,
                options.CancellationToken);

            if (!write.Success)
            {
                return CsoRepairResult.Fail(
                    NormalizeWriteError(write.ErrorCode),
                    write.ErrorMessage ?? "Streaming repair failed.",
                    format.ToString(),
                    mode: RepairMode.Streaming.ToString(),
                    usedTempIso: false);
            }

            return CsoRepairResult.Ok(
                format.ToString(),
                write.BytesRead,
                write.BytesWritten,
                paddingBytes: 0,
                mode: RepairMode.Streaming.ToString(),
                usedTempIso: false,
                codecTrialSummary: write.CodecTrialSummary,
                compressedBlocks: write.CompressedBlocks,
                storedBlocks: write.StoredBlocks,
                zeroBlocks: write.ZeroBlocks);
        }
        catch (BlockContainerReadException ex)
        {
            return CsoRepairResult.Fail(
                MapContainerReadError(ex),
                $"{ex.Code}: {ex.Message}",
                format.ToString(),
                mode: RepairMode.Streaming.ToString(),
                usedTempIso: false);
        }
        catch (InvalidDataException ex)
        {
            return CsoRepairResult.Fail(
                "RepairNotPossible",
                ex.Message,
                format.ToString(),
                mode: RepairMode.Streaming.ToString(),
                usedTempIso: false);
        }
        catch (IOException ex)
        {
            return CsoRepairResult.Fail(
                "IoError",
                ex.Message,
                format.ToString(),
                mode: RepairMode.Streaming.ToString(),
                usedTempIso: false);
        }
    }


    private static string NormalizeWriteError(string? errorCode)
    {
        return errorCode switch
        {
            "CsoDeepVerifyFailed" or "VerificationFailed" => "VerificationFailed",
            "NativeZopfliUnavailable" => "NativeCodecUnavailable",
            "CompressionIoFailed" => "IoError",
            null or "" => "RepairNotPossible",
            _ => errorCode,
        };
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
}
