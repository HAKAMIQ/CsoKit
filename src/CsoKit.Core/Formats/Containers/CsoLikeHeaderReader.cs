using System.Buffers.Binary;
using CsoKit.Core.Formats.Cso;

namespace CsoKit.Core.Formats.Containers;

internal static class CsoLikeHeaderReader
{
    public static (CsoHeader Header, IReadOnlyList<CsoIndexEntry> Entries) Read(
        FileStream input,
        ReadOnlySpan<byte> expectedMagic,
        Func<byte, bool> acceptsVersion,
        string formatName)
    {
        Span<byte> headerBytes = stackalloc byte[CsoConstants.MinimumHeaderSize];
        ReadExactly(input, headerBytes);

        if (!headerBytes[..4].SequenceEqual(expectedMagic))
        {
            throw new BlockContainerReadException(
                "InvalidMagic",
                $"{formatName} magic did not match the expected container signature.");
        }

        uint headerSize = BinaryPrimitives.ReadUInt32LittleEndian(headerBytes[4..8]);
        ulong uncompressedSize = BinaryPrimitives.ReadUInt64LittleEndian(headerBytes[8..16]);
        uint blockSize = BinaryPrimitives.ReadUInt32LittleEndian(headerBytes[16..20]);
        byte version = headerBytes[20];
        byte indexShift = headerBytes[21];

        if (!acceptsVersion(version))
        {
            throw new BlockContainerReadException(
                "UnsupportedContainer",
                $"{formatName} version {version} is not supported for safe normalization.");
        }

        if (uncompressedSize == 0)
        {
            throw new BlockContainerReadException(
                "InvalidUncompressedSize",
                $"{formatName} uncompressed size is zero.");
        }

        if (blockSize == 0 || blockSize > CsoConstants.MaxSupportedBlockSize)
        {
            throw new BlockContainerReadException(
                "InvalidBlockSize",
                $"{formatName} block size is invalid or too large.");
        }

        if (indexShift > CsoConstants.MaxSupportedIndexShift)
        {
            throw new BlockContainerReadException(
                "InvalidIndexShift",
                $"{formatName} index shift is invalid.");
        }

        CsoHeader header = new(headerSize, uncompressedSize, blockSize, version, indexShift);

        if (header.IndexEntryCount > int.MaxValue)
        {
            throw new BlockContainerReadException(
                "TooManyIndexEntries",
                $"{formatName} index table is too large.");
        }

        long indexStart = version == 2
            ? checked((long)header.EffectiveHeaderSize)
            : CsoConstants.MinimumHeaderSize;

        long indexEnd = checked(indexStart + header.IndexTableSizeBytes);

        if (input.Length < indexEnd)
        {
            throw new BlockContainerReadException(
                "IndexTableTruncated",
                $"{formatName} index table is truncated.");
        }

        input.Position = indexStart;

        int entryCount = checked((int)header.IndexEntryCount);
        CsoIndexEntry[] entries = new CsoIndexEntry[entryCount];
        Span<byte> rawEntry = stackalloc byte[sizeof(uint)];

        for (int i = 0; i < entryCount; i++)
        {
            ReadExactly(input, rawEntry);
            uint rawValue = BinaryPrimitives.ReadUInt32LittleEndian(rawEntry);
            entries[i] = CsoIndexEntry.FromRaw(i, rawValue, header.IndexShift);
        }

        if (version == 2 && entries[^1].HasFlag)
        {
            throw new BlockContainerReadException(
                "CsoV2FinalSentinelHighBit",
                $"{formatName} final sentinel index entry must not have the high-bit flag set.",
                blockIndex: entries.Length - 1);
        }

        if (entries[0].Offset < (ulong)indexEnd)
        {
            throw new BlockContainerReadException(
                "FirstDataOffsetBeforeIndexEnd",
                $"{formatName} first data offset points before the end of the index table.",
                blockIndex: 0);
        }

        for (int i = 1; i < entries.Length; i++)
        {
            if (entries[i].Offset < entries[i - 1].Offset)
            {
                throw new BlockContainerReadException(
                    "IndexOffsetsNotMonotonic",
                    $"{formatName} index offsets are not monotonic at block {i - 1}.",
                    blockIndex: i - 1);
            }
        }

        if (entries[^1].Offset > (ulong)input.Length)
        {
            throw new BlockContainerReadException(
                "FinalOffsetPastEndOfFile",
                $"{formatName} final offset points past the end of the file.",
                blockIndex: entries.Length - 1);
        }

        return (header, entries);
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
                    "Container header or index ended unexpectedly.");
            }

            total += read;
        }
    }
}