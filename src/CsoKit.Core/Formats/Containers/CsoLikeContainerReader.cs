using CsoKit.Core.Formats.Cso;
using CsoKit.Core.Formats.DiscImage;

namespace CsoKit.Core.Formats.Containers;

public abstract class CsoLikeContainerReader : IBlockContainerReader
{
    private readonly FileStream input;
    private readonly CsoHeader header;
    private readonly IReadOnlyList<CsoIndexEntry> entries;
    private bool disposed;

    protected CsoLikeContainerReader(
        string inputPath,
        string expectedMagic,
        Func<byte, bool> acceptsVersion,
        string formatName)
    {
        FileStream stream = new(
            inputPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 1024 * 128,
            FileOptions.RandomAccess);

        try
        {
            byte[] magicBytes = GetAsciiBytes(expectedMagic);
            (header, entries) = CsoLikeHeaderReader.Read(stream, magicBytes, acceptsVersion, formatName);
            input = stream;
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    public abstract DetectedDiscFormat Format { get; }

    public ulong UncompressedSize => header.UncompressedSize;

    public uint BlockSize => header.BlockSize;

    public int BlockCount => checked((int)header.SectorCount);

    /// <summary>File offset where the last block's data ends.</summary>
    public ulong DataEndOffset
    {
        get
        {
            ulong sentinel = entries[^1].Offset;
            ulong fileLength = (ulong)input.Length;
            ulong alignment = 1UL << header.IndexShift;

            return FinalBlockMayEndPastSentinel &&
                fileLength > sentinel &&
                fileLength - sentinel < alignment
                    ? fileLength
                    : sentinel;
        }
    }

    /// <summary>True when the file holds bytes after the last block's data.</summary>
    public bool HasTrailingData => DataEndOffset != (ulong)input.Length;

    /// <summary>
    /// Writers that store the final sentinel as <c>end &gt;&gt; index_shift</c> without padding
    /// first (ziso.py) round the end down, so the last block runs up to
    /// <c>(1 &lt;&lt; index_shift) - 1</c> bytes past the sentinel, to end of file.
    /// </summary>
    protected virtual bool FinalBlockMayEndPastSentinel => false;

    protected CsoHeader Header => header;

    public int ReadBlock(int blockIndex, Span<byte> output)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        if ((uint)blockIndex >= (uint)BlockCount)
        {
            throw new ArgumentOutOfRangeException(nameof(blockIndex));
        }

        int expectedBytes = GetExpectedBlockBytes(blockIndex);

        if (output.Length < expectedBytes)
        {
            throw new ArgumentException("Output buffer is smaller than the decoded block.", nameof(output));
        }

        CsoIndexEntry current = entries[blockIndex];
        ulong nextOffset = blockIndex + 1 == BlockCount
            ? DataEndOffset
            : entries[blockIndex + 1].Offset;

        if (nextOffset < current.Offset)
        {
            throw new BlockContainerReadException(
                "IndexOffsetsNotMonotonic",
                $"{Format} index offsets are not monotonic at block {blockIndex}.",
                blockIndex);
        }

        ulong physicalSize = nextOffset - current.Offset;

        if (current.Offset > (ulong)input.Length ||
            nextOffset > (ulong)input.Length)
        {
            throw new BlockContainerReadException(
                "IndexOffsetPastEndOfFile",
                $"{Format} block {blockIndex} points past the end of the file.",
                blockIndex);
        }

        input.Position = checked((long)current.Offset);
        return ReadPayload(input, current, physicalSize, blockIndex, output[..expectedBytes]);
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected abstract int ReadPayload(
        FileStream input,
        CsoIndexEntry current,
        ulong physicalSize,
        int blockIndex,
        Span<byte> output);

    protected virtual void Dispose(bool disposing)
    {
        if (disposed)
        {
            return;
        }

        if (disposing)
        {
            input.Dispose();
        }

        disposed = true;
    }

    protected static byte[] ReadPayloadBytes(
        FileStream input,
        ulong physicalSize,
        int blockIndex,
        string formatName)
    {
        if (physicalSize == 0 || physicalSize > int.MaxValue)
        {
            throw new BlockContainerReadException(
                "InvalidCompressedBlockSize",
                $"{formatName} block {blockIndex} has an invalid payload size.",
                blockIndex);
        }

        byte[] payload = new byte[checked((int)physicalSize)];
        ReadExactly(input, payload);
        return payload;
    }

    protected static int ReadStored(
        FileStream input,
        ulong physicalSize,
        int blockIndex,
        Span<byte> output,
        string formatName)
    {
        if (physicalSize < (ulong)output.Length)
        {
            throw new BlockContainerReadException(
                "StoredBlockTooSmall",
                $"{formatName} stored block {blockIndex} is smaller than the expected decoded bytes.",
                blockIndex);
        }

        ReadExactly(input, output);
        return output.Length;
    }

    /// <summary>
    /// Decodes a raw LZ4 block payload into <paramref name="output"/>, which holds the
    /// logical bytes of the block. Accepts index-alignment padding of any byte value and
    /// a final partial block that the writer zero-filled and compressed as a full block
    /// (maxcso does this whenever the block size is larger than one sector).
    /// </summary>
    protected int DecodeLz4Payload(
        ReadOnlySpan<byte> payload,
        int blockIndex,
        Span<byte> output,
        string formatName)
    {
        int maxPadding = (int)Math.Min(int.MaxValue, (1L << header.IndexShift) - 1);

        if (Lz4BlockDecoder.TryDecode(payload, output, out int bytesWritten, maxPadding) &&
            bytesWritten == output.Length)
        {
            return bytesWritten;
        }

        if (output.Length < header.BlockSize)
        {
            byte[] fullBlock = new byte[header.BlockSize];

            if (Lz4BlockDecoder.TryDecode(payload, fullBlock, out int fullBytes, maxPadding) &&
                fullBytes == fullBlock.Length)
            {
                fullBlock.AsSpan(0, output.Length).CopyTo(output);
                return output.Length;
            }
        }

        throw new BlockContainerReadException(
            "CorruptCompressedBlock",
            $"{formatName} LZ4 block {blockIndex} could not be decoded. Re-dump required.",
            blockIndex);
    }

    protected int InflateRawDeflate(
        ReadOnlySpan<byte> payload,
        int blockIndex,
        Span<byte> output,
        string formatName)
    {
        if (!RawDeflateVerifier.TryInflateBlock(payload, output, checked((int)header.BlockSize), out int bytesWritten) ||
            bytesWritten != output.Length)
        {
            throw new BlockContainerReadException(
                "CorruptCompressedBlock",
                $"{formatName} raw deflate block {blockIndex} could not be decoded. Re-dump required.",
                blockIndex);
        }

        return bytesWritten;
    }

    private static byte[] GetAsciiBytes(string value)
    {
        byte[] bytes = new byte[value.Length];

        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];

            if (character > 0x7F)
            {
                throw new ArgumentException("Container magic must be ASCII.", nameof(value));
            }

            bytes[index] = (byte)character;
        }

        return bytes;
    }

    private int GetExpectedBlockBytes(int blockIndex)
    {
        ulong start = checked((ulong)blockIndex * header.BlockSize);
        ulong remaining = header.UncompressedSize - start;

        return checked((int)Math.Min(header.BlockSize, remaining));
    }

    private static void ReadExactly(Stream input, Span<byte> output)
    {
        int total = 0;

        while (total < output.Length)
        {
            int read = input.Read(output[total..]);

            if (read == 0)
            {
                throw new BlockContainerReadException(
                    "UnexpectedEndOfFile",
                    "Container payload ended unexpectedly.");
            }

            total += read;
        }
    }
}