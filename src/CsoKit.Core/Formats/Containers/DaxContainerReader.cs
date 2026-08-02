using System.Buffers.Binary;
using System.IO.Compression;
using CsoKit.Core.Formats.DiscImage;

namespace CsoKit.Core.Formats.Containers;

public sealed class DaxContainerReader : IBlockContainerReader
{
    private const int HeaderSize = 32;
    private const uint DaxBlockSize = 8192;

    private readonly FileStream input;
    private readonly uint[] offsets;
    private readonly ushort[] sizes;
    private readonly DaxNonCompressedArea[] nonCompressedAreas;

    public DaxContainerReader(string inputPath)
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
            Span<byte> header = stackalloc byte[HeaderSize];
            ReadExactly(stream, header);

            if (!header[..3].SequenceEqual("DAX"u8))
            {
                throw new BlockContainerReadException(
                    "InvalidMagic",
                    "DAX magic did not match the expected container signature.");
            }

            uint uncompressedSize = BinaryPrimitives.ReadUInt32LittleEndian(header[4..8]);
            uint version = BinaryPrimitives.ReadUInt32LittleEndian(header[8..12]);
            uint nonCompressedAreaCount = BinaryPrimitives.ReadUInt32LittleEndian(header[12..16]);

            if (version > 1)
            {
                throw new BlockContainerReadException(
                    "UnsupportedContainer",
                    $"DAX version {version} is not supported for safe normalization.");
            }

            if (uncompressedSize == 0)
            {
                throw new BlockContainerReadException(
                    "InvalidUncompressedSize",
                    "DAX uncompressed size is zero.");
            }

            UncompressedSize = uncompressedSize;
            BlockSize = DaxBlockSize;
            BlockCount = checked((int)((UncompressedSize + DaxBlockSize - 1) / DaxBlockSize));

            offsets = new uint[BlockCount];
            sizes = new ushort[BlockCount];

            Span<byte> offsetBytes = stackalloc byte[sizeof(uint)];

            for (int i = 0; i < offsets.Length; i++)
            {
                ReadExactly(stream, offsetBytes);
                offsets[i] = BinaryPrimitives.ReadUInt32LittleEndian(offsetBytes);
            }

            Span<byte> sizeBytes = stackalloc byte[sizeof(ushort)];

            for (int i = 0; i < sizes.Length; i++)
            {
                ReadExactly(stream, sizeBytes);
                sizes[i] = BinaryPrimitives.ReadUInt16LittleEndian(sizeBytes);
            }

            if (version == 0 || nonCompressedAreaCount == 0)
            {
                nonCompressedAreas = [];
                input = stream;
                return;
            }

            if (nonCompressedAreaCount > 1024)
            {
                throw new BlockContainerReadException(
                    "UnsupportedContainer",
                    "DAX non-compressed area table is unreasonably large.");
            }

            nonCompressedAreas = new DaxNonCompressedArea[checked((int)nonCompressedAreaCount)];
            Span<byte> areaBytes = stackalloc byte[sizeof(uint) * 2];

            for (int i = 0; i < nonCompressedAreas.Length; i++)
            {
                ReadExactly(stream, areaBytes);

                uint start = BinaryPrimitives.ReadUInt32LittleEndian(areaBytes[..4]);
                uint count = BinaryPrimitives.ReadUInt32LittleEndian(areaBytes[4..8]);
                ulong end = (ulong)start + count;

                if (count == 0 || start >= (uint)BlockCount || end > (uint)BlockCount)
                {
                    throw new BlockContainerReadException(
                        "InvalidNonCompressedArea",
                        "DAX non-compressed area points outside the decoded block range.");
                }

                nonCompressedAreas[i] = new DaxNonCompressedArea(start, count);
            }

            input = stream;
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    public DetectedDiscFormat Format => DetectedDiscFormat.Dax;

    public ulong UncompressedSize { get; }

    public uint BlockSize { get; }

    public int BlockCount { get; }

    public int ReadBlock(int blockIndex, Span<byte> output)
    {
        if ((uint)blockIndex >= (uint)BlockCount)
        {
            throw new ArgumentOutOfRangeException(nameof(blockIndex));
        }

        int expectedBytes = GetExpectedBlockBytes(blockIndex);

        if (output.Length < expectedBytes)
        {
            throw new ArgumentException("Output buffer is smaller than the decoded block.", nameof(output));
        }

        uint offset = offsets[blockIndex];

        if (offset >= input.Length)
        {
            throw new BlockContainerReadException(
                "IndexOffsetPastEndOfFile",
                $"DAX block {blockIndex} points past the end of the file.",
                blockIndex);
        }

        input.Position = offset;

        if (IsNonCompressed(blockIndex))
        {
            ReadExactly(input, output[..expectedBytes]);
            return expectedBytes;
        }

        int compressedSize = sizes[blockIndex];

        if (compressedSize <= 0)
        {
            throw new BlockContainerReadException(
                "InvalidCompressedBlockSize",
                $"DAX block {blockIndex} has an invalid compressed size.",
                blockIndex);
        }

        if ((ulong)offset + (ulong)compressedSize > (ulong)input.Length)
        {
            throw new BlockContainerReadException(
                "IndexOffsetPastEndOfFile",
                $"DAX block {blockIndex} payload points past the end of the file.",
                blockIndex);
        }

        byte[] compressed = new byte[compressedSize];
        ReadExactly(input, compressed);

        try
        {
            using MemoryStream compressedStream = new(compressed, writable: false);
            using ZLibStream zlib = new(compressedStream, CompressionMode.Decompress);
            int read = ReadExactlyOrLess(zlib, output[..expectedBytes]);

            if (read != expectedBytes)
            {
                throw new BlockContainerReadException(
                    "CorruptCompressedBlock",
                    $"DAX zlib block {blockIndex} produced {read} bytes, expected {expectedBytes}. Re-dump required.",
                    blockIndex);
            }

            Span<byte> extra = stackalloc byte[1];

            if (zlib.Read(extra) != 0)
            {
                throw new BlockContainerReadException(
                    "CorruptCompressedBlock",
                    $"DAX zlib block {blockIndex} produced more bytes than expected. Re-dump required.",
                    blockIndex);
            }

            return read;
        }
        catch (InvalidDataException ex)
        {
            throw new BlockContainerReadException(
                "CorruptCompressedBlock",
                $"DAX zlib block {blockIndex} could not be decoded. Re-dump required.",
                blockIndex,
                ex);
        }
        catch (IOException ex)
        {
            throw new BlockContainerReadException(
                "CorruptCompressedBlock",
                $"DAX zlib block {blockIndex} could not be decoded. Re-dump required.",
                blockIndex,
                ex);
        }
    }

    public void Dispose()
    {
        input.Dispose();
    }

    private int GetExpectedBlockBytes(int blockIndex)
    {
        ulong start = checked((ulong)blockIndex * DaxBlockSize);
        ulong remaining = UncompressedSize - start;

        return checked((int)Math.Min(DaxBlockSize, remaining));
    }

    private bool IsNonCompressed(int blockIndex)
    {
        foreach (DaxNonCompressedArea area in nonCompressedAreas)
        {
            ulong end = (ulong)area.Start + area.Count;

            if ((ulong)blockIndex >= area.Start && (ulong)blockIndex < end)
            {
                return true;
            }
        }

        return false;
    }

    private static void ReadExactly(Stream input, Span<byte> output)
    {
        int read = ReadExactlyOrLess(input, output);

        if (read != output.Length)
        {
            throw new BlockContainerReadException(
                "UnexpectedEndOfFile",
                "DAX input ended unexpectedly.");
        }
    }

    private static int ReadExactlyOrLess(Stream input, Span<byte> output)
    {
        int total = 0;

        while (total < output.Length)
        {
            int read = input.Read(output[total..]);

            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return total;
    }

    private readonly record struct DaxNonCompressedArea(uint Start, uint Count);
}