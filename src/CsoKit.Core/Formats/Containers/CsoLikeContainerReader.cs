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
        CsoIndexEntry next = entries[blockIndex + 1];

        if (next.Offset < current.Offset)
        {
            throw new BlockContainerReadException(
                "IndexOffsetsNotMonotonic",
                $"{Format} index offsets are not monotonic at block {blockIndex}.",
                blockIndex);
        }

        ulong physicalSize = next.Offset - current.Offset;

        if (current.Offset > (ulong)input.Length ||
            next.Offset > (ulong)input.Length)
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

    protected static int InflateRawDeflate(
        ReadOnlySpan<byte> payload,
        int blockIndex,
        Span<byte> output,
        string formatName)
    {
        if (!RawDeflateVerifier.TryInflate(payload, output, out int bytesWritten) ||
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