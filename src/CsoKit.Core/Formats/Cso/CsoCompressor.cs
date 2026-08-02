using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using CsoKit.Core.Compression;
using CsoKit.Core.Compression.Trials;
using CsoKit.Core.Native;

namespace CsoKit.Core.Formats.Cso;

public sealed class CsoCompressor
{
    public const uint DefaultBlockSize = 2048;

    private const ulong OutputSafetyBufferBytes = 64UL * 1024UL * 1024UL;
    private const int ProgressReportBlockInterval = 256;

    private readonly CsoOutputSafetyPolicy outputSafetyPolicy = new();
    private readonly CsoDiskSpacePreflight diskSpacePreflight = new();

    public CsoCompressResult Compress(CsoCompressOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        CancellationToken cancellationToken = options.CancellationToken;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!File.Exists(options.InputPath))
            {
                return CsoCompressResult.Fail("InputNotFound", "Input file was not found.");
            }

            CsoOutputSafetyResult outputSafety = outputSafetyPolicy.Validate(
                options.InputPath,
                options.OutputPath,
                options.ForceOverwrite);

            if (!outputSafety.Success)
            {
                return CsoCompressResult.Fail(
                    outputSafety.ErrorCode ?? "OutputSafetyFailed",
                    outputSafety.ErrorMessage ?? "Output safety validation failed.");
            }

            if (options.BlockSize == 0)
            {
                return CsoCompressResult.Fail("InvalidBlockSize", "CSO block size is zero.");
            }

            if (options.BlockSize < DefaultBlockSize)
            {
                return CsoCompressResult.Fail(
                    "InvalidBlockSize",
                    $"CSO block size must be at least {DefaultBlockSize:N0} bytes.");
            }

            if (!IsPowerOfTwo(options.BlockSize))
            {
                return CsoCompressResult.Fail("InvalidBlockSize", "CSO block size must be a power of two.");
            }

            if (options.BlockSize > CsoConstants.MaxSupportedBlockSize)
            {
                return CsoCompressResult.Fail(
                    "BlockSizeTooLarge",
                    $"CSO block size is too large. Maximum supported block size is {CsoConstants.MaxSupportedBlockSize:N0} bytes.");
            }

            if (!CsoWorkerPolicy.TryValidate(options.WorkerCount, out string? workerError))
            {
                return CsoCompressResult.Fail("InvalidThreadCount", workerError ?? "Compression worker count is invalid.");
            }

            if (options.CollectCodecReport && options.CodecReportBlockLimit < 0)
            {
                return CsoCompressResult.Fail("InvalidCodecReportBlockLimit", "Codec report block limit cannot be negative.");
            }

            if (options.UseZopfli && !NativeCsoRuntime.GetInfo().IsAvailable)
            {
                return CsoCompressResult.Fail(
                    "NativeZopfliUnavailable",
                    "Zopfli compression requires the native backend DLL to be available.");
            }

            FileInfo inputInfo = new(options.InputPath);

            if (inputInfo.Length <= 0)
            {
                return CsoCompressResult.Fail("InvalidInputSize", "Input ISO file is empty.");
            }

            ulong inputBytes = checked((ulong)inputInfo.Length);
            int blockSize = checked((int)options.BlockSize);
            int totalBlocks = checked((int)((inputBytes + options.BlockSize - 1) / options.BlockSize));
            ulong indexBytes = checked((ulong)(totalBlocks + 1) * sizeof(uint));
            ulong headerAndIndexBytes = checked((ulong)CsoConstants.MinimumHeaderSize + indexBytes);
            ulong requiredBytes = AddSafetyBuffer(checked(inputBytes + headerAndIndexBytes));
            byte indexShift = CsoIndexShiftPolicy.ComputeShift(checked(inputBytes + headerAndIndexBytes));

            if (indexShift > 0)
            {
                return CsoCompressResult.Fail(
                    "IndexShiftRequired",
                    "This ISO can require shifted CSO indexes. The game-safe CSO1 writer currently keeps index shift at 0; use a smaller input or wait for explicit large-compatible output.");
            }

            CsoDiskSpacePreflightResult diskSpace = diskSpacePreflight.CheckOutputSpace(options.OutputPath, requiredBytes);

            if (!diskSpace.Success)
            {
                return CsoCompressResult.Fail(
                    diskSpace.ErrorCode ?? "DiskSpacePreflightFailed",
                    diskSpace.ErrorMessage ?? "Disk space preflight failed.");
            }

            string fullOutputPath = Path.GetFullPath(options.OutputPath);
            string tempOutputPath = CreateUniqueTempOutputPath(fullOutputPath);

            try
            {
                CsoCompressResult result;

                using (FileStream input = new(
                    options.InputPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 1024 * 128,
                    FileOptions.SequentialScan))
                {
                    using FileStream output = new(
                        tempOutputPath,
                        FileMode.CreateNew,
                        FileAccess.ReadWrite,
                        FileShare.None,
                        bufferSize: 1024 * 128,
                        FileOptions.SequentialScan);

                    result = CompressBlocks(
                        input,
                        output,
                        inputBytes,
                        blockSize,
                        totalBlocks,
                        indexShift,
                        options.Profile,
                        options.WorkerCount,
                        options.UseZopfli,
                        options.CollectCodecReport,
                        options.CodecReportBlockLimit,
                        cancellationToken,
                        options.Progress);

                    output.Flush(true);
                }

                cancellationToken.ThrowIfCancellationRequested();

                if (options.DeepVerifyOutput)
                {
                    CsoDeepVerifyResult deepVerify = new CsoDeepVerifier().Verify(
                        tempOutputPath,
                        computeSha256: false,
                        cancellationToken: cancellationToken);

                    if (!deepVerify.Success)
                    {
                        CsoDeepVerifyIssue? issue = deepVerify.Issues.Count > 0
    ? deepVerify.Issues[0]
    : null;
                        SafeDelete(tempOutputPath);

                        return CsoCompressResult.Fail(
                            issue?.Code ?? "CsoDeepVerifyFailed",
                            issue?.Message ?? "CSO deep verification failed.");
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();
                File.Move(tempOutputPath, fullOutputPath, overwrite: options.ForceOverwrite);

                return result;
            }
            catch (OperationCanceledException)
            {
                SafeDelete(tempOutputPath);
                return CsoCompressResult.Fail("OperationCanceled", "Operation was canceled.");
            }
            catch (InvalidDataException ex)
            {
                SafeDelete(tempOutputPath);
                return CsoCompressResult.Fail("CompressionFailed", ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                SafeDelete(tempOutputPath);
                return CsoCompressResult.Fail("OutputAccessDenied", ex.Message);
            }
            catch (IOException ex)
            {
                SafeDelete(tempOutputPath);
                return CsoCompressResult.Fail("CompressionIoFailed", ex.Message);
            }
        }
        catch (OperationCanceledException)
        {
            return CsoCompressResult.Fail("OperationCanceled", "Operation was canceled.");
        }
    }

    private static CsoCompressResult CompressBlocks(
        FileStream input,
        FileStream output,
        ulong inputBytes,
        int blockSize,
        int totalBlocks,
        byte indexShift,
        CsoCompressionProfile profile,
        int workerCount,
        bool useZopfli,
        bool collectCodecReport,
        int codecReportBlockLimit,
        CancellationToken cancellationToken,
        IProgress<CsoCompressProgress>? progress)
    {
        int effectiveWorkerCount = Math.Min(workerCount, totalBlocks);

        if (effectiveWorkerCount <= 1)
        {
            return CompressBlocksSequential(
                input,
                output,
                inputBytes,
                blockSize,
                totalBlocks,
                indexShift,
                profile,
                useZopfli,
                collectCodecReport,
                codecReportBlockLimit,
                cancellationToken,
                progress);
        }

        return CompressBlocksParallel(
            input,
            output,
            inputBytes,
            blockSize,
            totalBlocks,
            indexShift,
            profile,
            effectiveWorkerCount,
            useZopfli,
            collectCodecReport,
            codecReportBlockLimit,
            cancellationToken,
            progress);
    }

    private static CsoCompressResult CompressBlocksSequential(
        FileStream input,
        FileStream output,
        ulong inputBytes,
        int blockSize,
        int totalBlocks,
        byte indexShift,
        CsoCompressionProfile profile,
        bool useZopfli,
        bool collectCodecReport,
        int codecReportBlockLimit,
        CancellationToken cancellationToken,
        IProgress<CsoCompressProgress>? progress)
    {
        ulong dataStart = checked((ulong)CsoConstants.MinimumHeaderSize + ((ulong)(totalBlocks + 1) * sizeof(uint)));

        CsoIndexBuilder indexBuilder = new(totalBlocks, indexShift);
        CsoOrderedOutputWriter outputWriter = new(output);
        CsoCompressionWorker compressionWorker = new(profile, useZopfli, collectTrialReports: collectCodecReport);
        byte[] inputBuffer = new byte[blockSize];

        outputWriter.ReserveDataStart(dataStart);

        ulong totalRead = 0;
        int compressedBlocks = 0;
        int storedBlocks = 0;
        int zeroBlocks = 0;
        Dictionary<string, int> codecWins = new(StringComparer.OrdinalIgnoreCase);
        CodecTrialSummaryBuilder? trialSummaryBuilder = collectCodecReport
            ? new CodecTrialSummaryBuilder(codecReportBlockLimit)
            : null;

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
            ulong blockOffset = outputWriter.Position;

            indexBuilder.AddSectorOffset(blockOffset, sectorResult.IsStored);
            outputWriter.Write(sectorResult);

            if (sectorResult.IsStored)
            {
                storedBlocks++;
            }
            else
            {
                compressedBlocks++;
            }

            if (sectorResult.SourceIsAllZero)
            {
                zeroBlocks++;
            }

            IncrementCodecWin(codecWins, sectorResult);
            AddTrialReport(trialSummaryBuilder, sectorResult);

            totalRead += (ulong)read;

            int completedBlocks = blockIndex + 1;

            if (completedBlocks == totalBlocks ||
                completedBlocks % ProgressReportBlockInterval == 0)
            {
                ReportProgress(progress, completedBlocks, totalBlocks, totalRead, inputBytes);
            }
        }

        indexBuilder.AddFinalOffset(outputWriter.Position);

        ulong bytesWritten = outputWriter.Position;

        WriteHeaderAndIndex(
            output,
            inputBytes,
            (uint)blockSize,
            indexShift,
            indexBuilder.Entries);

        ReportProgress(progress, totalBlocks, totalBlocks, totalRead, inputBytes);

        return CsoCompressResult.Ok(
            totalRead,
            bytesWritten,
            compressedBlocks,
            storedBlocks,
            codecWins,
            trialSummaryBuilder?.Build(codecWins),
            zeroBlocks);
    }

    private static CsoCompressResult CompressBlocksParallel(
        FileStream input,
        FileStream output,
        ulong inputBytes,
        int blockSize,
        int totalBlocks,
        byte indexShift,
        CsoCompressionProfile profile,
        int workerCount,
        bool useZopfli,
        bool collectCodecReport,
        int codecReportBlockLimit,
        CancellationToken cancellationToken,
        IProgress<CsoCompressProgress>? progress)
    {
        ulong dataStart = checked((ulong)CsoConstants.MinimumHeaderSize + ((ulong)(totalBlocks + 1) * sizeof(uint)));

        CsoIndexBuilder indexBuilder = new(totalBlocks, indexShift);
        CsoOrderedOutputWriter outputWriter = new(output);
        outputWriter.ReserveDataStart(dataStart);

        using CancellationTokenSource pipelineCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        int queueCapacity = CsoWorkerPolicy.GetBoundedQueueCapacity(workerCount);
        using BlockingCollection<SectorJob> jobs = new(boundedCapacity: queueCapacity);
        using BlockingCollection<SectorResult> results = new(boundedCapacity: queueCapacity);

        object failureLock = new();
        ExceptionDispatchInfo? failure = null;

        void CaptureFailure(Exception exception)
        {
            lock (failureLock)
            {
                failure ??= ExceptionDispatchInfo.Capture(exception);
            }

            pipelineCancellation.Cancel();
        }

        ExceptionDispatchInfo? GetFailure()
        {
            lock (failureLock)
            {
                return failure;
            }
        }

        CancellationToken pipelineToken = pipelineCancellation.Token;

        ReportProgress(progress, completedBlocks: 0, totalBlocks, bytesRead: 0, inputBytes);

        Task producer = Task.Run(() =>
        {
            try
            {
                ulong totalRead = 0;
                byte[] inputBuffer = new byte[blockSize];

                for (int blockIndex = 0; blockIndex < totalBlocks; blockIndex++)
                {
                    pipelineToken.ThrowIfCancellationRequested();

                    int expectedBytes = checked((int)Math.Min((ulong)blockSize, inputBytes - totalRead));
                    int read = CsoBlockReader.ReadExactlyOrLess(input, inputBuffer.AsSpan(0, expectedBytes));

                    if (read != expectedBytes)
                    {
                        throw new EndOfStreamException($"Unexpected end of ISO. Expected {expectedBytes:N0} bytes, got {read:N0}.");
                    }

                    byte[] blockBuffer = inputBuffer.AsSpan(0, read).ToArray();
                    SectorJob job = new(
                        blockIndex,
                        totalRead,
                        read,
                        blockBuffer);

                    jobs.Add(job, pipelineToken);
                    totalRead += (ulong)read;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || GetFailure() is not null)
            {
            }
            catch (Exception ex)
            {
                CaptureFailure(ex);
            }
            finally
            {
                jobs.CompleteAdding();
            }
        });

        Task[] workers = new Task[workerCount];

        for (int workerIndex = 0; workerIndex < workers.Length; workerIndex++)
        {
            workers[workerIndex] = Task.Run(() =>
            {
                try
                {
                    CsoCompressionWorker compressionWorker = new(profile, useZopfli, collectTrialReports: collectCodecReport);

                    foreach (SectorJob job in jobs.GetConsumingEnumerable(pipelineToken))
                    {
                        SectorResult result = compressionWorker.Compress(job);
                        results.Add(result, pipelineToken);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || GetFailure() is not null)
                {
                }
                catch (Exception ex)
                {
                    CaptureFailure(ex);
                }
            });
        }

        Task resultCloser = Task.Run(() =>
        {
            try
            {
                Task.WaitAll(workers);
            }
            catch (AggregateException ex)
            {
                foreach (Exception inner in ex.Flatten().InnerExceptions)
                {
                    if (inner is not OperationCanceledException || (!cancellationToken.IsCancellationRequested && GetFailure() is null))
                    {
                        CaptureFailure(inner);
                        break;
                    }
                }
            }
            finally
            {
                results.CompleteAdding();
            }
        });

        SortedDictionary<int, SectorResult> pendingResults = [];
        int nextBlockToWrite = 0;
        ulong totalWrittenSourceBytes = 0;
        int compressedBlocks = 0;
        int storedBlocks = 0;
        int zeroBlocks = 0;
        Dictionary<string, int> codecWins = new(StringComparer.OrdinalIgnoreCase);
        CodecTrialSummaryBuilder? trialSummaryBuilder = collectCodecReport
            ? new CodecTrialSummaryBuilder(codecReportBlockLimit)
            : null;

        try
        {
            while (nextBlockToWrite < totalBlocks)
            {
                ThrowIfFailure(GetFailure());
                cancellationToken.ThrowIfCancellationRequested();

                if (!pendingResults.TryGetValue(nextBlockToWrite, out SectorResult? nextResult))
                {
                    try
                    {
                        if (results.TryTake(out SectorResult? result, millisecondsTimeout: 100, pipelineToken))
                        {
                            if (result.BlockIndex < nextBlockToWrite || pendingResults.ContainsKey(result.BlockIndex))
                            {
                                throw new InvalidDataException("Compression pipeline returned a duplicate or stale sector result.");
                            }

                            pendingResults.Add(result.BlockIndex, result);
                        }
                        else if (results.IsCompleted)
                        {
                            throw new InvalidDataException("Compression pipeline completed before all sector results were written.");
                        }

                        continue;
                    }
                    catch (OperationCanceledException) when (GetFailure() is not null)
                    {
                        ThrowIfFailure(GetFailure());
                    }
                }

                if (nextResult is null)
                {
                    throw new InvalidDataException("Compression pipeline returned an empty sector result.");
                }

                pendingResults.Remove(nextBlockToWrite);

                ulong blockOffset = outputWriter.Position;
                indexBuilder.AddSectorOffset(blockOffset, nextResult.IsStored);
                outputWriter.Write(nextResult);

                if (nextResult.IsStored)
                {
                    storedBlocks++;
                }
                else
                {
                    compressedBlocks++;
                }

                if (nextResult.SourceIsAllZero)
                {
                    zeroBlocks++;
                }

                IncrementCodecWin(codecWins, nextResult);
                AddTrialReport(trialSummaryBuilder, nextResult);

                totalWrittenSourceBytes += (ulong)nextResult.SourceLength;
                nextBlockToWrite++;

                if (nextBlockToWrite == totalBlocks ||
                    nextBlockToWrite % ProgressReportBlockInterval == 0)
                {
                    ReportProgress(progress, nextBlockToWrite, totalBlocks, totalWrittenSourceBytes, inputBytes);
                }
            }

            producer.Wait();
            resultCloser.Wait();
            ThrowIfFailure(GetFailure());

            indexBuilder.AddFinalOffset(outputWriter.Position);

            ulong bytesWritten = outputWriter.Position;

            WriteHeaderAndIndex(
                output,
                inputBytes,
                (uint)blockSize,
                indexShift,
                indexBuilder.Entries);

            ReportProgress(progress, totalBlocks, totalBlocks, totalWrittenSourceBytes, inputBytes);

            return CsoCompressResult.Ok(
                totalWrittenSourceBytes,
                bytesWritten,
                compressedBlocks,
                storedBlocks,
                codecWins,
                trialSummaryBuilder?.Build(codecWins),
                zeroBlocks);
        }
        finally
        {
            pipelineCancellation.Cancel();
            WaitForPipelineTask(producer);
            WaitForPipelineTask(resultCloser);
        }
    }

    private static void AddTrialReport(
        CodecTrialSummaryBuilder? summaryBuilder,
        SectorResult result)
    {
        if (result.TrialReport is not null)
        {
            summaryBuilder?.Add(result.TrialReport);
        }
    }

    private static void ThrowIfFailure(ExceptionDispatchInfo? failure)
    {
        failure?.Throw();
    }

    private static void WaitForPipelineTask(Task task)
    {
        try
        {
            task.Wait();
        }
        catch
        {
        }
    }

    private static void WriteHeaderAndIndex(
        FileStream output,
        ulong uncompressedSize,
        uint blockSize,
        byte indexShift,
        IReadOnlyList<uint> indexEntries)
    {
        Span<byte> header = stackalloc byte[CsoConstants.MinimumHeaderSize];

        header[0] = (byte)'C';
        header[1] = (byte)'I';
        header[2] = (byte)'S';
        header[3] = (byte)'O';

        BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(4, 4), CsoConstants.MinimumHeaderSize);
        BinaryPrimitives.WriteUInt64LittleEndian(header.Slice(8, 8), uncompressedSize);
        BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(16, 4), blockSize);

        header[20] = 1;
        header[21] = indexShift;
        header[22] = 0;
        header[23] = 0;

        output.Position = 0;
        output.Write(header);

        Span<byte> rawEntry = stackalloc byte[sizeof(uint)];

        foreach (uint entry in indexEntries)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(rawEntry, entry);
            output.Write(rawEntry);
        }
    }

    private static ulong AddSafetyBuffer(ulong requiredBytes)
    {
        if (requiredBytes > ulong.MaxValue - OutputSafetyBufferBytes)
        {
            return ulong.MaxValue;
        }

        return requiredBytes + OutputSafetyBufferBytes;
    }

    private static void IncrementCodecWin(
        Dictionary<string, int> codecWins,
        SectorResult result)
    {
        string codecName = result.EffectiveCodecName;

        if (codecWins.TryGetValue(codecName, out int current))
        {
            codecWins[codecName] = checked(current + 1);
            return;
        }

        codecWins.Add(codecName, 1);
    }

    private static bool IsPowerOfTwo(uint value)
    {
        return value != 0 && (value & (value - 1)) == 0;
    }

    private static string CreateUniqueTempOutputPath(string fullOutputPath)
    {
        string directory = Path.GetDirectoryName(fullOutputPath) ?? ".";
        string fileName = Path.GetFileName(fullOutputPath);

        return Path.Combine(directory, $".{fileName}.{Guid.NewGuid():N}.tmp");
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
