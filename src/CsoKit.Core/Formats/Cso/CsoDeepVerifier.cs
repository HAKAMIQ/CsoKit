using System.Buffers;
using System.Security.Cryptography;

namespace CsoKit.Core.Formats.Cso;

public sealed class CsoDeepVerifier
{
    private readonly CsoVerifier verifier = new();

    public CsoDeepVerifyResult Verify(
        string inputPath,
        bool computeSha256,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(inputPath))
        {
            return CsoDeepVerifyResult.Fail(
                null,
                blocksChecked: 0,
                bytesReconstructed: 0,
                [new CsoDeepVerifyIssue("InvalidInputPath", "Input path is empty.")]);
        }

        if (!File.Exists(inputPath))
        {
            return CsoDeepVerifyResult.Fail(
                null,
                blocksChecked: 0,
                bytesReconstructed: 0,
                [new CsoDeepVerifyIssue("InputNotFound", "Input file was not found.")]);
        }

        CsoVerificationResult shallow = verifier.Verify(inputPath);
        cancellationToken.ThrowIfCancellationRequested();

        if (!shallow.Success || shallow.Header is null || shallow.Entries.Count == 0)
        {
            return CsoDeepVerifyResult.Fail(
                shallow.Header,
                blocksChecked: 0,
                bytesReconstructed: 0,
                ConvertIssues(shallow.Issues)) with
            {
                FileLength = GetFileLengthOrNull(inputPath),
                VerificationScope = "Legacy structural CSO header/index validation",
                ModernLayer = "Not reached because structural validation failed.",
            };
        }

        if (shallow.Header.Version is not (0 or 1))
        {
            return CsoDeepVerifyResult.Fail(
                shallow.Header,
                blocksChecked: 0,
                bytesReconstructed: 0,
                [new CsoDeepVerifyIssue(
                    "UnsupportedContainer",
                    "CsoDeepVerifier is limited to CSO1 semantics. Use ContainerDeepVerifier for CSO2/ZSO/DAX input.")]) with
            {
                FileLength = GetFileLengthOrNull(inputPath),
            };
        }

        try
        {
            using FileStream input = new(
                inputPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 1024 * 128,
                FileOptions.SequentialScan);

            return Verify(input, shallow.Header, shallow.Entries, computeSha256, cancellationToken);
        }
        catch (UnauthorizedAccessException ex)
        {
            return CsoDeepVerifyResult.Fail(
                shallow.Header,
                blocksChecked: 0,
                bytesReconstructed: 0,
                [new CsoDeepVerifyIssue("InputAccessDenied", ex.Message)]) with
            {
                FileLength = GetFileLengthOrNull(inputPath),
            };
        }
        catch (IOException ex)
        {
            return CsoDeepVerifyResult.Fail(
                shallow.Header,
                blocksChecked: 0,
                bytesReconstructed: 0,
                [new CsoDeepVerifyIssue("DeepVerifyIoFailed", ex.Message)]) with
            {
                FileLength = GetFileLengthOrNull(inputPath),
            };
        }
    }

    public static CsoDeepVerifyResult Verify(
        Stream input,
        CsoHeader header,
        IReadOnlyList<CsoIndexEntry> entries,
        bool computeSha256,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(entries);

        if (!input.CanRead || !input.CanSeek)
        {
            return Decorate(
                CsoDeepVerifyResult.Fail(
                    header,
                    blocksChecked: 0,
                    bytesReconstructed: 0,
                    [new CsoDeepVerifyIssue("StreamNotSeekable", "Deep verification requires a readable seekable stream.")]),
                input,
                header,
                entries,
                compressedBlocks: 0,
                storedBlocks: 0,
                zeroBlocks: 0,
                payloadBlocksDecoded: 0,
                physicalPayloadBytes: 0);
        }

        if (header.Version is not (0 or 1))
        {
            return Decorate(
                CsoDeepVerifyResult.Fail(
                    header,
                    blocksChecked: 0,
                    bytesReconstructed: 0,
                    [new CsoDeepVerifyIssue(
                        "UnsupportedContainer",
                        "CsoDeepVerifier is limited to CSO1 semantics. Use ContainerDeepVerifier for CSO2/ZSO/DAX input.")]),
                input,
                header,
                entries,
                compressedBlocks: 0,
                storedBlocks: 0,
                zeroBlocks: 0,
                payloadBlocksDecoded: 0,
                physicalPayloadBytes: 0);
        }

        if (entries.Count != header.IndexEntryCount)
        {
            return Decorate(
                CsoDeepVerifyResult.Fail(
                    header,
                    blocksChecked: 0,
                    bytesReconstructed: 0,
                    [new CsoDeepVerifyIssue(
                        "IndexEntryCountMismatch",
                        $"Expected {header.IndexEntryCount:N0} index entries, got {entries.Count:N0}.")]),
                input,
                header,
                entries,
                compressedBlocks: 0,
                storedBlocks: 0,
                zeroBlocks: 0,
                payloadBlocksDecoded: 0,
                physicalPayloadBytes: 0);
        }

        if (entries[^1].HasFlag)
        {
            return Decorate(
                CsoDeepVerifyResult.Fail(
                    header,
                    blocksChecked: 0,
                    bytesReconstructed: 0,
                    [new CsoDeepVerifyIssue("FinalIndexEntryHasFlag", "Final CSO index entry must not carry the stored-block flag.")]),
                input,
                header,
                entries,
                compressedBlocks: 0,
                storedBlocks: 0,
                zeroBlocks: 0,
                payloadBlocksDecoded: 0,
                physicalPayloadBytes: 0);
        }

        if (entries[^1].Offset != (ulong)input.Length)
        {
            return Decorate(
                CsoDeepVerifyResult.Fail(
                    header,
                    blocksChecked: 0,
                    bytesReconstructed: 0,
                    [new CsoDeepVerifyIssue(
                        "FinalOffsetMismatch",
                        $"Final CSO offset is {entries[^1].Offset:N0}, but file length is {input.Length:N0}.")]),
                input,
                header,
                entries,
                compressedBlocks: 0,
                storedBlocks: 0,
                zeroBlocks: 0,
                payloadBlocksDecoded: 0,
                physicalPayloadBytes: 0);
        }

        int blockSize = checked((int)header.BlockSize);
        byte[] outputBuffer = ArrayPool<byte>.Shared.Rent(blockSize);
        byte[]? compressedBuffer = null;

        using IncrementalHash? hash = computeSha256
            ? IncrementalHash.CreateHash(HashAlgorithmName.SHA256)
            : null;

        int blocksChecked = 0;
        int compressedBlocks = 0;
        int storedBlocks = 0;
        int zeroBlocks = 0;
        ulong bytesReconstructed = 0;
        ulong physicalPayloadBytes = 0;

        try
        {
            for (int blockIndex = 0; blockIndex < header.SectorCount; blockIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CsoIndexEntry current = entries[blockIndex];
                CsoIndexEntry next = entries[blockIndex + 1];

                if (next.Offset < current.Offset)
                {
                    return Decorate(
                        FailAt(header, blocksChecked, bytesReconstructed, "IndexOffsetsNotMonotonic", blockIndex),
                        input,
                        header,
                        entries,
                        compressedBlocks,
                        storedBlocks,
                        zeroBlocks,
                        blocksChecked,
                        physicalPayloadBytes);
                }

                ulong remaining = header.UncompressedSize - ((ulong)blockIndex * header.BlockSize);
                int expectedBytes = checked((int)Math.Min(header.BlockSize, remaining));
                ulong physicalSize = next.Offset - current.Offset;

                if (expectedBytes <= 0)
                {
                    return Decorate(
                        FailAt(header, blocksChecked, bytesReconstructed, "InvalidExpectedBlockSize", blockIndex),
                        input,
                        header,
                        entries,
                        compressedBlocks,
                        storedBlocks,
                        zeroBlocks,
                        blocksChecked,
                        physicalPayloadBytes);
                }

                if (current.Offset > (ulong)input.Length)
                {
                    return Decorate(
                        FailAt(header, blocksChecked, bytesReconstructed, "IndexOffsetPastEndOfFile", blockIndex),
                        input,
                        header,
                        entries,
                        compressedBlocks,
                        storedBlocks,
                        zeroBlocks,
                        blocksChecked,
                        physicalPayloadBytes);
                }

                input.Position = checked((long)current.Offset);
                Span<byte> output = outputBuffer.AsSpan(0, expectedBytes);

                if (current.HasFlag)
                {
                    if (physicalSize < (ulong)expectedBytes)
                    {
                        return Decorate(
                            FailAt(header, blocksChecked, bytesReconstructed, "StoredBlockTooSmall", blockIndex),
                            input,
                            header,
                            entries,
                            compressedBlocks,
                            storedBlocks,
                            zeroBlocks,
                            blocksChecked,
                            physicalPayloadBytes);
                    }

                    ReadExactly(input, output);
                    storedBlocks++;
                }
                else
                {
                    if (physicalSize == 0 || physicalSize > int.MaxValue)
                    {
                        return Decorate(
                            FailAt(header, blocksChecked, bytesReconstructed, "InvalidCompressedBlockSize", blockIndex),
                            input,
                            header,
                            entries,
                            compressedBlocks,
                            storedBlocks,
                            zeroBlocks,
                            blocksChecked,
                            physicalPayloadBytes);
                    }

                    int compressedLength = checked((int)physicalSize);
                    compressedBuffer = EnsureRentedCapacity(compressedBuffer, compressedLength);
                    ReadExactly(input, compressedBuffer.AsSpan(0, compressedLength));

                    if (!RawDeflateVerifier.TryInflate(compressedBuffer.AsSpan(0, compressedLength), output, out int bytesWritten) ||
                        bytesWritten != expectedBytes)
                    {
                        return Decorate(
                            FailAt(header, blocksChecked, bytesReconstructed, "CorruptCompressedBlock", blockIndex),
                            input,
                            header,
                            entries,
                            compressedBlocks,
                            storedBlocks,
                            zeroBlocks,
                            blocksChecked,
                            physicalPayloadBytes);
                    }

                    compressedBlocks++;
                }

                if (IsAllZero(output))
                {
                    zeroBlocks++;
                }

                hash?.AppendData(output);
                bytesReconstructed += (ulong)expectedBytes;
                physicalPayloadBytes += physicalSize;
                blocksChecked++;
            }
        }
        catch (EndOfStreamException)
        {
            return Decorate(
                FailAt(header, blocksChecked, bytesReconstructed, "CsoDeepVerifyFailed", blocksChecked),
                input,
                header,
                entries,
                compressedBlocks,
                storedBlocks,
                zeroBlocks,
                blocksChecked,
                physicalPayloadBytes);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(outputBuffer);

            if (compressedBuffer is not null)
            {
                ArrayPool<byte>.Shared.Return(compressedBuffer);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (bytesReconstructed != header.UncompressedSize)
        {
            return Decorate(
                CsoDeepVerifyResult.Fail(
                    header,
                    blocksChecked,
                    bytesReconstructed,
                    [new CsoDeepVerifyIssue(
                        "ReconstructedSizeMismatch",
                        $"Deep verify reconstructed {bytesReconstructed:N0} bytes, expected {header.UncompressedSize:N0}.")]),
                input,
                header,
                entries,
                compressedBlocks,
                storedBlocks,
                zeroBlocks,
                blocksChecked,
                physicalPayloadBytes);
        }

        string? sha256 = hash is null
            ? null
            : Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();

        return Decorate(
            CsoDeepVerifyResult.Ok(header, blocksChecked, bytesReconstructed, sha256),
            input,
            header,
            entries,
            compressedBlocks,
            storedBlocks,
            zeroBlocks,
            blocksChecked,
            physicalPayloadBytes);
    }

    private static CsoDeepVerifyIssue[] ConvertIssues(IReadOnlyList<CsoVerificationIssue> issues)
    {
        CsoDeepVerifyIssue[] convertedIssues = new CsoDeepVerifyIssue[issues.Count];

        for (int index = 0; index < issues.Count; index++)
        {
            CsoVerificationIssue issue = issues[index];
            convertedIssues[index] = new CsoDeepVerifyIssue(
                issue.Code,
                issue.Message,
                issue.BlockIndex);
        }

        return convertedIssues;
    }

    private static byte[] EnsureRentedCapacity(byte[]? buffer, int requiredLength)
    {
        if (buffer is not null && buffer.Length >= requiredLength)
        {
            return buffer;
        }

        if (buffer is not null)
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        return ArrayPool<byte>.Shared.Rent(requiredLength);
    }

    private static bool IsAllZero(ReadOnlySpan<byte> data)
    {
        return data.IndexOfAnyExcept((byte)0) < 0;
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

    private static CsoDeepVerifyResult Decorate(
        CsoDeepVerifyResult result,
        Stream input,
        CsoHeader header,
        IReadOnlyList<CsoIndexEntry> entries,
        int compressedBlocks,
        int storedBlocks,
        int zeroBlocks,
        int payloadBlocksDecoded,
        ulong physicalPayloadBytes)
    {
        long? indexTableBytes = null;
        long? indexEndOffset = null;

        try
        {
            indexTableBytes = header.IndexTableSizeBytes;
            indexEndOffset = checked((long)header.EffectiveHeaderSize + header.IndexTableSizeBytes);
        }
        catch (OverflowException)
        {
            // The result already carries the actual verification failure. Diagnostics stay partial.
        }

        return result with
        {
            AlgorithmName = "Hybrid CSO verification",
            VerificationScope = "Header + index + block payload reconstruction",
            LegacyLayer = "Legacy structural CSO header/index validation",
            ModernLayer = "Streaming payload decode with pooled compressed buffers",
            ForensicLayer = "Coverage, topology, bounds, and reconstruction diagnostics",
            FileLength = input.CanSeek ? input.Length : null,
            HeaderSize = header.EffectiveHeaderSize,
            IndexEntryCount = entries.Count,
            IndexTableBytes = indexTableBytes,
            IndexEndOffset = indexEndOffset,
            FirstDataOffset = entries.Count > 0 ? ToNullableLong(entries[0].Offset) : null,
            FinalDataOffset = entries.Count > 0 ? ToNullableLong(entries[^1].Offset) : null,
            TotalBlocks = header.SectorCount,
            ExpectedReconstructedBytes = header.UncompressedSize,
            PhysicalPayloadBytes = physicalPayloadBytes,
            CompressedBlocks = compressedBlocks,
            StoredBlocks = storedBlocks,
            ZeroBlocks = zeroBlocks,
            PayloadBlocksDecoded = payloadBlocksDecoded,
        };
    }

    private static long? ToNullableLong(ulong value)
    {
        return value <= long.MaxValue ? (long)value : null;
    }

    private static CsoDeepVerifyIssue[] CreateIssue(string code, string message, int? blockIndex = null)
    {
        return [new CsoDeepVerifyIssue(code, message, blockIndex)];
    }

    private static CsoDeepVerifyResult FailAt(
        CsoHeader header,
        int blocksChecked,
        ulong bytesReconstructed,
        string code,
        int blockIndex)
    {
        string message = code switch
        {
            "IndexOffsetsNotMonotonic" => $"CSO index offsets are not monotonic at block {blockIndex:N0}.",
            "StoredBlockTooSmall" => $"Stored CSO block {blockIndex:N0} is smaller than the expected sector payload.",
            "InvalidCompressedBlockSize" => $"Compressed CSO block {blockIndex:N0} has an invalid physical size.",
            "CorruptCompressedBlock" => $"Compressed CSO block {blockIndex:N0} could not be inflated as raw deflate. Diagnosis: Re-dump required.",
            "IndexOffsetPastEndOfFile" => $"CSO block {blockIndex:N0} points past the end of file.",
            "InvalidExpectedBlockSize" => $"CSO block {blockIndex:N0} has an invalid expected decoded byte count.",
            _ => $"CSO deep verification failed at block {blockIndex:N0}.",
        };

        return CsoDeepVerifyResult.Fail(
            header,
            blocksChecked,
            bytesReconstructed,
            CreateIssue(code, message, blockIndex));
    }

    private static void ReadExactly(Stream stream, Span<byte> buffer)
    {
        int totalRead = 0;

        while (totalRead < buffer.Length)
        {
            int read = stream.Read(buffer[totalRead..]);

            if (read == 0)
            {
                throw new EndOfStreamException();
            }

            totalRead += read;
        }
    }
}
