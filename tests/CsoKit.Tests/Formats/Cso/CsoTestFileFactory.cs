using System.Buffers.Binary;
using System.IO.Compression;
using CsoKit.Core.Formats.Cso;

namespace CsoKit.Tests.Formats.Cso;

internal static class CsoTestFileFactory
{
    public static string CreateTempCsoV1(
        byte[] originalBytes,
        uint blockSize = 2048,
        byte version = 1,
        byte indexShift = 0,
        Func<int, bool>? storeBlockUncompressed = null,
        uint? headerSizeOverride = null)
    {
        if (originalBytes.Length == 0)
        {
            throw new ArgumentException("Original bytes must not be empty.", nameof(originalBytes));
        }

        int blockSizeInt = checked((int)blockSize);
        int sectorCount = checked((originalBytes.Length + blockSizeInt - 1) / blockSizeInt);
        int indexEntryCount = checked(sectorCount + 1);
        int dataStart = checked(CsoConstants.MinimumHeaderSize + (indexEntryCount * sizeof(uint)));

        List<uint> rawEntries = [];
        List<byte> payload = [];

        ulong currentOffset = checked((ulong)dataStart);

        for (int blockIndex = 0; blockIndex < sectorCount; blockIndex++)
        {
            int sourceOffset = checked(blockIndex * blockSizeInt);
            int sourceLength = Math.Min(blockSizeInt, originalBytes.Length - sourceOffset);
            byte[] block = originalBytes[sourceOffset..(sourceOffset + sourceLength)];
            bool storeUncompressed = storeBlockUncompressed?.Invoke(blockIndex) ?? false;

            rawEntries.Add(EncodeIndexOffset(currentOffset, indexShift, storeUncompressed));

            byte[] storedBlock = storeUncompressed
                ? block
                : CompressRawDeflate(block);

            payload.AddRange(storedBlock);
            currentOffset += checked((ulong)storedBlock.Length);
        }

        rawEntries.Add(EncodeIndexOffset(currentOffset, indexShift, hasFlag: false));

        byte[] bytes = new byte[checked(dataStart + payload.Count)];
        WriteHeader(
            bytes,
            headerSizeOverride ?? CsoConstants.MinimumHeaderSize,
            checked((ulong)originalBytes.Length),
            blockSize,
            version,
            indexShift);

        int indexOffset = CsoConstants.MinimumHeaderSize;

        foreach (uint rawEntry in rawEntries)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(indexOffset, sizeof(uint)), rawEntry);
            indexOffset += sizeof(uint);
        }

        payload.CopyTo(bytes, dataStart);

        string path = Path.Combine(Path.GetTempPath(), $"CsoKit_{Guid.NewGuid():N}.cso");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    public static string CreateTempZso(byte[] originalBytes, uint blockSize = 2048)
    {
        return CreateTempCsoLike(
            originalBytes,
            magic: "ZISO",
            version: 1,
            blockSize,
            encodeBlock: EncodeLz4Block,
            hasFlag: static _ => false,
            extension: ".zso");
    }

    public static string CreateTempCso2(byte[] originalBytes, uint blockSize = 2048)
    {
        return CreateTempCsoLike(
            originalBytes,
            magic: "CISO",
            version: 2,
            blockSize,
            encodeBlock: EncodeLz4Block,
            hasFlag: static _ => true,
            extension: ".cso");
    }

    public static string CreateTempDax(byte[] originalBytes)
    {
        if (originalBytes.Length == 0)
        {
            throw new ArgumentException("Original bytes must not be empty.", nameof(originalBytes));
        }

        const int headerSize = 32;
        const int blockSize = 8192;

        int frameCount = checked((originalBytes.Length + blockSize - 1) / blockSize);
        int tableSize = checked((frameCount * sizeof(uint)) + (frameCount * sizeof(ushort)));
        int dataStart = checked(headerSize + tableSize);

        List<byte[]> frames = [];
        uint[] offsets = new uint[frameCount];
        ushort[] sizes = new ushort[frameCount];
        int currentOffset = dataStart;

        for (int blockIndex = 0; blockIndex < frameCount; blockIndex++)
        {
            int sourceOffset = checked(blockIndex * blockSize);
            int sourceLength = Math.Min(blockSize, originalBytes.Length - sourceOffset);
            byte[] block = originalBytes[sourceOffset..(sourceOffset + sourceLength)];
            byte[] compressed = CompressZLib(block);

            if (compressed.Length > ushort.MaxValue)
            {
                throw new InvalidOperationException("DAX test compressed frame is too large.");
            }

            offsets[blockIndex] = checked((uint)currentOffset);
            sizes[blockIndex] = checked((ushort)compressed.Length);
            frames.Add(compressed);
            currentOffset += compressed.Length;
        }

        byte[] bytes = new byte[currentOffset];
        bytes[0] = (byte)'D';
        bytes[1] = (byte)'A';
        bytes[2] = (byte)'X';
        bytes[3] = 0;

        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4, 4), checked((uint)originalBytes.Length));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8, 4), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12, 4), 0);

        int cursor = headerSize;

        foreach (uint offset in offsets)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(cursor, sizeof(uint)), offset);
            cursor += sizeof(uint);
        }

        foreach (ushort size in sizes)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(cursor, sizeof(ushort)), size);
            cursor += sizeof(ushort);
        }

        foreach (byte[] frame in frames)
        {
            frame.CopyTo(bytes, cursor);
            cursor += frame.Length;
        }

        string path = Path.Combine(Path.GetTempPath(), $"CsoKit_{Guid.NewGuid():N}.dax");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    public static string CreateUnsupportedZsoHeader()
    {
        return CreateUnsupportedCsoLikeHeader("ZISO", version: 9, extension: ".zso");
    }

    public static string CreateUnsupportedCso2Header()
    {
        return CreateUnsupportedCsoLikeHeader("CISO", version: 2, extension: ".cso", blockSize: 0);
    }

    public static string CreateUnsupportedDaxHeader()
    {
        byte[] bytes = new byte[32];
        bytes[0] = (byte)'D';
        bytes[1] = (byte)'A';
        bytes[2] = (byte)'X';
        bytes[3] = 0;
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4, 4), 4096);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8, 4), 2);

        string path = Path.Combine(Path.GetTempPath(), $"CsoKit_{Guid.NewGuid():N}.dax");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static uint EncodeIndexOffset(
        ulong offset,
        byte indexShift,
        bool hasFlag)
    {
        ulong shiftedOffset = offset >> indexShift;

        if ((shiftedOffset << indexShift) != offset)
        {
            throw new ArgumentException("CSO test offset is not aligned with index_shift.", nameof(indexShift));
        }

        if (shiftedOffset > CsoIndexEntry.OffsetMask)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "CSO test offset does not fit in a v1 index entry.");
        }

        uint rawValue = checked((uint)shiftedOffset);

        if (hasFlag)
        {
            rawValue |= CsoIndexEntry.FlagMask;
        }

        return rawValue;
    }

    private static byte[] CompressRawDeflate(byte[] block)
    {
        using MemoryStream compressed = new();

        using (DeflateStream deflate = new(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            deflate.Write(block);
        }

        return compressed.ToArray();
    }

    private static string CreateTempCsoLike(
        byte[] originalBytes,
        string magic,
        byte version,
        uint blockSize,
        Func<byte[], byte[]> encodeBlock,
        Func<int, bool> hasFlag,
        string extension)
    {
        if (originalBytes.Length == 0)
        {
            throw new ArgumentException("Original bytes must not be empty.", nameof(originalBytes));
        }

        int blockSizeInt = checked((int)blockSize);
        int sectorCount = checked((originalBytes.Length + blockSizeInt - 1) / blockSizeInt);
        int indexEntryCount = checked(sectorCount + 1);
        int dataStart = checked(CsoConstants.MinimumHeaderSize + (indexEntryCount * sizeof(uint)));

        List<uint> rawEntries = [];
        List<byte> payload = [];
        ulong currentOffset = checked((ulong)dataStart);

        for (int blockIndex = 0; blockIndex < sectorCount; blockIndex++)
        {
            int sourceOffset = checked(blockIndex * blockSizeInt);
            int sourceLength = Math.Min(blockSizeInt, originalBytes.Length - sourceOffset);
            byte[] block = originalBytes[sourceOffset..(sourceOffset + sourceLength)];
            byte[] encoded = encodeBlock(block);

            rawEntries.Add(EncodeIndexOffset(currentOffset, indexShift: 0, hasFlag: hasFlag(blockIndex)));
            payload.AddRange(encoded);
            currentOffset += checked((ulong)encoded.Length);
        }

        rawEntries.Add(EncodeIndexOffset(currentOffset, indexShift: 0, hasFlag: false));

        byte[] bytes = new byte[checked(dataStart + payload.Count)];
        bytes[0] = (byte)magic[0];
        bytes[1] = (byte)magic[1];
        bytes[2] = (byte)magic[2];
        bytes[3] = (byte)magic[3];

        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4, 4), CsoConstants.MinimumHeaderSize);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(8, 8), checked((ulong)originalBytes.Length));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16, 4), blockSize);
        bytes[20] = version;
        bytes[21] = 0;

        int indexOffset = CsoConstants.MinimumHeaderSize;

        foreach (uint rawEntry in rawEntries)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(indexOffset, sizeof(uint)), rawEntry);
            indexOffset += sizeof(uint);
        }

        payload.CopyTo(bytes, dataStart);

        string path = Path.Combine(Path.GetTempPath(), $"CsoKit_{Guid.NewGuid():N}{extension}");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static string CreateUnsupportedCsoLikeHeader(
        string magic,
        byte version,
        string extension,
        uint blockSize = 2048)
    {
        byte[] bytes = new byte[CsoConstants.MinimumHeaderSize];
        bytes[0] = (byte)magic[0];
        bytes[1] = (byte)magic[1];
        bytes[2] = (byte)magic[2];
        bytes[3] = (byte)magic[3];

        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4, 4), CsoConstants.MinimumHeaderSize);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(8, 8), 4096);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16, 4), blockSize);
        bytes[20] = version;

        string path = Path.Combine(Path.GetTempPath(), $"CsoKit_{Guid.NewGuid():N}{extension}");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static byte[] CompressZLib(byte[] block)
    {
        using MemoryStream compressed = new();

        using (ZLibStream zlib = new(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            zlib.Write(block);
        }

        return compressed.ToArray();
    }

    private static byte[] EncodeLz4Block(byte[] block)
    {
        if (block.Length >= 5 && IsAllZero(block))
        {
            return EncodeLz4ZeroRun(block.Length);
        }

        return EncodeLz4LiteralOnly(block);
    }

    private static bool IsAllZero(ReadOnlySpan<byte> block)
    {
        foreach (byte value in block)
        {
            if (value != 0)
            {
                return false;
            }
        }

        return true;
    }

    private static byte[] EncodeLz4ZeroRun(int length)
    {
        using MemoryStream output = new();
        int matchLength = length - 1;
        int encodedMatchLength = matchLength - 4;
        int tokenMatchLength = Math.Min(15, encodedMatchLength);

        output.WriteByte((byte)((1 << 4) | tokenMatchLength));
        output.WriteByte(0);
        output.WriteByte(1);
        output.WriteByte(0);

        if (encodedMatchLength >= 15)
        {
            WriteExtendedLz4Length(output, encodedMatchLength - 15);
        }

        return output.ToArray();
    }

    private static byte[] EncodeLz4LiteralOnly(byte[] block)
    {
        using MemoryStream output = new();
        int tokenLiteralLength = Math.Min(15, block.Length);

        output.WriteByte((byte)(tokenLiteralLength << 4));

        if (block.Length >= 15)
        {
            WriteExtendedLz4Length(output, block.Length - 15);
        }

        output.Write(block);
        return output.ToArray();
    }

    private static void WriteExtendedLz4Length(Stream output, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        while (length >= 255)
        {
            output.WriteByte(255);
            length -= 255;
        }

        output.WriteByte((byte)length);
    }

    private static void WriteHeader(
        byte[] bytes,
        uint headerSize,
        ulong uncompressedSize,
        uint blockSize,
        byte version,
        byte indexShift)
    {
        bytes[0] = (byte)'C';
        bytes[1] = (byte)'I';
        bytes[2] = (byte)'S';
        bytes[3] = (byte)'O';

        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4, 4), headerSize);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(8, 8), uncompressedSize);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16, 4), blockSize);

        bytes[20] = version;
        bytes[21] = indexShift;
        bytes[22] = 0;
        bytes[23] = 0;
    }
}