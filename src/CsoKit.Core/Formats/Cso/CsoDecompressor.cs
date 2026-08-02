using System.IO.Compression;

namespace CsoKit.Core.Formats.Cso;

public sealed class CsoDecompressor
{
    private const ulong OutputSafetyBufferBytes = 64UL * 1024UL * 1024UL;
    private const int ProgressReportBlockInterval = 256;

    private readonly CsoVerifier verifier = new();
    private readonly CsoOutputSafetyPolicy outputSafetyPolicy = new();
    private readonly CsoDiskSpacePreflight diskSpacePreflight = new();

    public CsoDecompressResult Decompress(CsoDecompressOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        CancellationToken cancellationToken = options.CancellationToken;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!File.Exists(options.InputPath))
            {
                return CsoDecompressResult.Fail("InputNotFound", "Input file was not found.");
            }

            CsoOutputSafetyResult outputSafety = outputSafetyPolicy.Validate(
                options.InputPath,
                options.OutputPath,
                options.ForceOverwrite);

            if (!outputSafety.Success)
            {
                return CsoDecompressResult.Fail(
                    outputSafety.ErrorCode ?? "OutputSafetyFailed",
                    outputSafety.ErrorMessage ?? "Output safety validation failed.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            CsoVerificationResult verification = verifier.Verify(options.InputPath);

            if (!verification.Success || verification.Header is null || verification.Entries.Count == 0)
            {
                string message = "CSO verification failed.";
                string code = "VerificationFailed";

                if (verification.Issues.Count > 0)
                {
                    message = verification.Issues[0].Message;
                    code = verification.Issues[0].Code;
                }

                return CsoDecompressResult.Fail(code, message);
            }

            CsoHeader header = verification.Header;

            if (!header.IsCsoV1)
            {
                return CsoDecompressResult.Fail(
                    "UnsupportedDecompressionVersion",
                    "Unsupported CSO file.");
            }

            if (header.BlockSize > CsoConstants.MaxSupportedBlockSize)
            {
                return CsoDecompressResult.Fail(
                    "BlockSizeTooLarge",
                    $"CSO block size is too large. Maximum supported block size is {CsoConstants.MaxSupportedBlockSize:N0} bytes.");
            }

            ulong requiredBytes = AddSafetyBuffer(header.UncompressedSize);
            CsoDiskSpacePreflightResult diskSpace = diskSpacePreflight.CheckOutputSpace(options.OutputPath, requiredBytes);

            if (!diskSpace.Success)
            {
                return CsoDecompressResult.Fail(
                    diskSpace.ErrorCode ?? "DiskSpacePreflightFailed",
                    diskSpace.ErrorMessage ?? "Disk space preflight failed.");
            }

            string fullOutputPath = Path.GetFullPath(options.OutputPath);
            string tempOutputPath = CreateUniqueTempOutputPath(fullOutputPath);

            try
            {
                ulong bytesWritten;

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
                        FileAccess.Write,
                        FileShare.None,
                        bufferSize: 1024 * 128,
                        FileOptions.SequentialScan);

                    bytesWritten = DecompressBlocks(
                        input,
                        output,
                        header,
                        verification.Entries,
                        cancellationToken,
                        options.Progress);

                    output.Flush(true);
                }

                cancellationToken.ThrowIfCancellationRequested();

                File.Move(tempOutputPath, fullOutputPath, overwrite: options.ForceOverwrite);

                return CsoDecompressResult.Ok(bytesWritten);
            }
            catch (OperationCanceledException)
            {
                SafeDelete(tempOutputPath);
                return CsoDecompressResult.Fail("OperationCanceled", "Operation was canceled.");
            }
            catch (InvalidDataException ex)
            {
                SafeDelete(tempOutputPath);
                return CsoDecompressResult.Fail("InvalidCompressedData", ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                SafeDelete(tempOutputPath);
                return CsoDecompressResult.Fail("OutputAccessDenied", ex.Message);
            }
            catch (IOException ex)
            {
                SafeDelete(tempOutputPath);
                return CsoDecompressResult.Fail("DecompressionIoFailed", ex.Message);
            }
        }
        catch (OperationCanceledException)
        {
            return CsoDecompressResult.Fail("OperationCanceled", "Operation was canceled.");
        }
    }

    private static string CreateUniqueTempOutputPath(string fullOutputPath)
    {
        string directory = Path.GetDirectoryName(fullOutputPath) ?? ".";
        string fileName = Path.GetFileName(fullOutputPath);

        return Path.Combine(directory, $".{fileName}.{Guid.NewGuid():N}.tmp");
    }

    private static ulong AddSafetyBuffer(ulong uncompressedSize)
    {
        if (uncompressedSize > ulong.MaxValue - OutputSafetyBufferBytes)
        {
            return ulong.MaxValue;
        }

        return uncompressedSize + OutputSafetyBufferBytes;
    }

    private static ulong DecompressBlocks(
        FileStream input,
        FileStream output,
        CsoHeader header,
        IReadOnlyList<CsoIndexEntry> entries,
        CancellationToken cancellationToken,
        IProgress<CsoDecompressProgress>? progress)
    {
        int blockSize = checked((int)header.BlockSize);
        byte[] outputBuffer = new byte[blockSize];
        ulong totalWritten = 0;
        int totalBlocks = checked((int)header.SectorCount);

        ReportProgress(progress, completedBlocks: 0, totalBlocks, totalWritten, header.UncompressedSize);

        for (int blockIndex = 0; blockIndex < totalBlocks; blockIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            CsoIndexEntry current = entries[blockIndex];
            CsoIndexEntry next = entries[blockIndex + 1];

            if (next.Offset < current.Offset)
            {
                throw new InvalidDataException($"CSO index offsets are not monotonic at block {blockIndex:N0}.");
            }

            ulong remaining = header.UncompressedSize - totalWritten;
            int expectedBlockBytes = checked((int)Math.Min((ulong)blockSize, remaining));

            if (expectedBlockBytes <= 0)
            {
                break;
            }

            input.Position = checked((long)current.Offset);

            if (current.HasFlag)
            {
                ReadExactly(input, outputBuffer.AsSpan(0, expectedBlockBytes));
                output.Write(outputBuffer, 0, expectedBlockBytes);
            }
            else
            {
                ulong compressedSize64 = next.Offset - current.Offset;

                if (compressedSize64 == 0 || compressedSize64 > int.MaxValue)
                {
                    throw new InvalidDataException($"Invalid compressed block size at block {blockIndex:N0}.");
                }

                int compressedSize = checked((int)compressedSize64);
                byte[] compressedBuffer = new byte[compressedSize];

                ReadExactly(input, compressedBuffer);

                int decompressed = DecompressBlock(
                    compressedBuffer,
                    outputBuffer.AsSpan(0, expectedBlockBytes),
                    blockIndex);

                if (decompressed != expectedBlockBytes)
                {
                    throw new InvalidDataException(
                        $"Decompressed block {blockIndex:N0} produced {decompressed:N0} bytes, expected {expectedBlockBytes:N0} bytes.");
                }

                output.Write(outputBuffer, 0, expectedBlockBytes);
            }

            totalWritten += (ulong)expectedBlockBytes;

            int completedBlocks = blockIndex + 1;

            if (completedBlocks == totalBlocks ||
                completedBlocks % ProgressReportBlockInterval == 0)
            {
                ReportProgress(progress, completedBlocks, totalBlocks, totalWritten, header.UncompressedSize);
            }
        }

        if (totalWritten != header.UncompressedSize)
        {
            throw new InvalidDataException(
                $"Decompressed size mismatch. Written {totalWritten:N0} bytes, expected {header.UncompressedSize:N0} bytes.");
        }

        ReportProgress(progress, totalBlocks, totalBlocks, totalWritten, header.UncompressedSize);

        return totalWritten;
    }

    private static void ReportProgress(
        IProgress<CsoDecompressProgress>? progress,
        int completedBlocks,
        int totalBlocks,
        ulong bytesWritten,
        ulong totalBytes)
    {
        progress?.Report(new CsoDecompressProgress(
            completedBlocks,
            totalBlocks,
            bytesWritten,
            totalBytes));
    }

    private static int DecompressBlock(
        byte[] compressedBuffer,
        Span<byte> outputBuffer,
        int blockIndex)
    {
        if (TryDecompressWithZLib(compressedBuffer, outputBuffer, out int zlibBytes) &&
            zlibBytes == outputBuffer.Length)
        {
            return zlibBytes;
        }

        if (TryDecompressWithDeflate(compressedBuffer, outputBuffer, out int deflateBytes) &&
            deflateBytes == outputBuffer.Length)
        {
            return deflateBytes;
        }

        throw new InvalidDataException(
            $"Compressed CSO block {blockIndex:N0} could not be decompressed as zlib or raw deflate.");
    }

    private static bool TryDecompressWithZLib(
        byte[] compressedBuffer,
        Span<byte> outputBuffer,
        out int bytesWritten)
    {
        bytesWritten = 0;

        try
        {
            using MemoryStream compressedStream = new(compressedBuffer);
            using ZLibStream zlib = new(compressedStream, CompressionMode.Decompress);

            bytesWritten = ReadExactlyOrLess(zlib, outputBuffer);
            return true;
        }
        catch (InvalidDataException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static bool TryDecompressWithDeflate(
        byte[] compressedBuffer,
        Span<byte> outputBuffer,
        out int bytesWritten)
    {
        bytesWritten = 0;

        try
        {
            using MemoryStream compressedStream = new(compressedBuffer);
            using DeflateStream deflate = new(compressedStream, CompressionMode.Decompress);

            bytesWritten = ReadExactlyOrLess(deflate, outputBuffer);
            return true;
        }
        catch (InvalidDataException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static void ReadExactly(Stream stream, Span<byte> buffer)
    {
        int read = ReadExactlyOrLess(stream, buffer);

        if (read != buffer.Length)
        {
            throw new EndOfStreamException($"Unexpected end of stream. Expected {buffer.Length:N0} bytes, got {read:N0}.");
        }
    }

    private static int ReadExactlyOrLess(Stream stream, Span<byte> buffer)
    {
        int totalRead = 0;

        while (totalRead < buffer.Length)
        {
            int read = stream.Read(buffer[totalRead..]);

            if (read == 0)
            {
                break;
            }

            totalRead += read;
        }

        return totalRead;
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
            // Best-effort cleanup only.
        }
    }
}