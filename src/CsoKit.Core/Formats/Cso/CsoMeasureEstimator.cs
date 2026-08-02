using System.Diagnostics.CodeAnalysis;
using CsoKit.Core.Native;

namespace CsoKit.Core.Formats.Cso;

public sealed class CsoMeasureEstimator
{
    private const int ProgressReportBlockInterval = 256;

    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "Keep the instance API stable for existing CLI/tests callers.")]
    public CsoMeasureResult Measure(CsoMeasureOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        CancellationToken cancellationToken = options.CancellationToken;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!File.Exists(options.InputPath))
            {
                return CsoMeasureResult.Fail("InputNotFound", "Input file was not found.");
            }

            if (options.BlockSize == 0)
            {
                return CsoMeasureResult.Fail("InvalidBlockSize", "CSO block size is zero.");
            }

            if (options.BlockSize < CsoCompressor.DefaultBlockSize)
            {
                return CsoMeasureResult.Fail(
                    "InvalidBlockSize",
                    $"CSO block size must be at least {CsoCompressor.DefaultBlockSize:N0} bytes.");
            }

            if (!IsPowerOfTwo(options.BlockSize))
            {
                return CsoMeasureResult.Fail("InvalidBlockSize", "CSO block size must be a power of two.");
            }

            if (options.BlockSize > CsoConstants.MaxSupportedBlockSize)
            {
                return CsoMeasureResult.Fail(
                    "BlockSizeTooLarge",
                    $"CSO block size is too large. Maximum supported block size is {CsoConstants.MaxSupportedBlockSize:N0} bytes.");
            }

            if (options.UseZopfli && !NativeCsoRuntime.GetInfo().IsAvailable)
            {
                return CsoMeasureResult.Fail(
                    "NativeZopfliUnavailable",
                    "Zopfli compression requires the native backend DLL to be available.");
            }

            FileInfo inputInfo = new(options.InputPath);

            if (inputInfo.Length <= 0)
            {
                return CsoMeasureResult.Fail("InvalidInputSize", "Input ISO file is empty.");
            }

            ulong inputBytes = checked((ulong)inputInfo.Length);
            int blockSize = checked((int)options.BlockSize);
            int totalBlocks = checked((int)((inputBytes + options.BlockSize - 1) / options.BlockSize));

            using FileStream input = new(
                options.InputPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 1024 * 128,
                FileOptions.SequentialScan);

            return MeasureBlocks(
                input,
                inputBytes,
                blockSize,
                totalBlocks,
                options.Profile,
                options.UseZopfli,
                cancellationToken,
                options.Progress);
        }
        catch (OperationCanceledException)
        {
            return CsoMeasureResult.Fail("OperationCanceled", "Operation was canceled.");
        }
        catch (UnauthorizedAccessException ex)
        {
            return CsoMeasureResult.Fail("InputAccessDenied", ex.Message);
        }
        catch (IOException ex)
        {
            return CsoMeasureResult.Fail("MeasureIoFailed", ex.Message);
        }
    }

    private static CsoMeasureResult MeasureBlocks(
        FileStream input,
        ulong inputBytes,
        int blockSize,
        int totalBlocks,
        CsoCompressionProfile profile,
        bool useZopfli,
        CancellationToken cancellationToken,
        IProgress<CsoCompressProgress>? progress)
    {
        ulong estimatedBytes = checked((ulong)CsoConstants.MinimumHeaderSize + ((ulong)(totalBlocks + 1) * sizeof(uint)));

        CsoCompressionWorker compressionWorker = new(profile, useZopfli);
        byte[] inputBuffer = new byte[blockSize];

        ulong totalRead = 0;
        int compressedBlocks = 0;
        int storedBlocks = 0;

        ReportProgress(progress, completedBlocks: 0, totalBlocks, totalRead, inputBytes);

        for (int blockIndex = 0; blockIndex < totalBlocks; blockIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int expectedBytes = checked((int)Math.Min((ulong)blockSize, inputBytes - totalRead));
            int read = CsoBlockReader.ReadExactlyOrLess(input, inputBuffer.AsSpan(0, expectedBytes));

            if (read != expectedBytes)
            {
                throw new EndOfStreamException($"Unexpected end of ISO. Expected {expectedBytes:N0} bytes, got {read:N0}.");
            }

            SectorJob job = new(
                blockIndex,
                totalRead,
                read,
                inputBuffer.AsMemory(0, read));

            SectorResult sectorResult = compressionWorker.Compress(job);

            estimatedBytes = checked(estimatedBytes + (ulong)sectorResult.OutputLength);

            if (sectorResult.IsStored)
            {
                storedBlocks++;
            }
            else
            {
                compressedBlocks++;
            }

            totalRead += (ulong)read;

            int completedBlocks = blockIndex + 1;

            if (completedBlocks == totalBlocks ||
                completedBlocks % ProgressReportBlockInterval == 0)
            {
                ReportProgress(progress, completedBlocks, totalBlocks, totalRead, inputBytes);
            }
        }

        ReportProgress(progress, totalBlocks, totalBlocks, totalRead, inputBytes);

        return CsoMeasureResult.Ok(
            inputBytes,
            estimatedBytes,
            totalBlocks,
            compressedBlocks,
            storedBlocks);
    }

    private static void ReportProgress(
        IProgress<CsoCompressProgress>? progress,
        int completedBlocks,
        int totalBlocks,
        ulong bytesRead,
        ulong totalBytes)
    {
        progress?.Report(new CsoCompressProgress(
            completedBlocks,
            totalBlocks,
            bytesRead,
            totalBytes));
    }

    private static bool IsPowerOfTwo(uint value)
    {
        return value != 0 && (value & (value - 1)) == 0;
    }
}