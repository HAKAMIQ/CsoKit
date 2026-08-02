using System.Buffers.Binary;
using CsoKit.Core.Formats.Cso;

namespace CsoKit.Tests.Formats.Cso;

public sealed class CsoIndexReaderTests
{
    [Fact]
    public void Read_WithValidIndex_ReturnsEntries()
    {
        byte[] bytes = CreateCsoLikeFile(
            uncompressedSize: 4096,
            blockSize: 2048,
            version: 1,
            indexShift: 0,
            rawEntries: [36, 100, 200],
            dataLength: 200);

        CsoHeaderReader headerReader = new();
        using MemoryStream headerStream = new(bytes);
        CsoHeaderReadResult headerResult = headerReader.Read(headerStream);

        Assert.True(headerResult.Success);
        Assert.NotNull(headerResult.Header);

        CsoIndexReader indexReader = new();
        using MemoryStream indexStream = new(bytes);
        CsoIndexReadResult indexResult = indexReader.Read(indexStream, headerResult.Header);

        Assert.True(indexResult.Success);
        Assert.Equal(3, indexResult.Entries.Count);
        Assert.Equal((ulong)36, indexResult.Entries[0].Offset);
        Assert.Equal((ulong)100, indexResult.Entries[1].Offset);
        Assert.Equal((ulong)200, indexResult.Entries[2].Offset);
    }


    [Fact]
    public void Read_WithUnreliableCsoV1HeaderSize_ReadsIndexFromEffectiveHeaderSize()
    {
        byte[] bytes = CreateCsoLikeFile(
            uncompressedSize: 4096,
            blockSize: 2048,
            version: 1,
            indexShift: 0,
            rawEntries: [36, 100, 200],
            dataLength: 200,
            headerSize: 4096);

        CsoHeaderReader headerReader = new();
        using MemoryStream headerStream = new(bytes);
        CsoHeaderReadResult headerResult = headerReader.Read(headerStream);

        Assert.True(headerResult.Success);
        Assert.NotNull(headerResult.Header);
        Assert.Equal((uint)4096, headerResult.Header.HeaderSize);
        Assert.Equal((uint)CsoConstants.MinimumHeaderSize, headerResult.Header.EffectiveHeaderSize);

        CsoIndexReader indexReader = new();
        using MemoryStream indexStream = new(bytes);
        CsoIndexReadResult indexResult = indexReader.Read(indexStream, headerResult.Header);

        Assert.True(indexResult.Success);
        Assert.Equal(3, indexResult.Entries.Count);
        Assert.Equal((ulong)36, indexResult.Entries[0].Offset);
    }

    [Fact]
    public void Read_WithTruncatedIndex_ReturnsIndexTableTruncated()
    {
        byte[] bytes = CreateHeaderOnly(
            uncompressedSize: 4096,
            blockSize: 2048,
            version: 1,
            indexShift: 0);

        CsoHeaderReader headerReader = new();
        using MemoryStream headerStream = new(bytes);
        CsoHeaderReadResult headerResult = headerReader.Read(headerStream);

        Assert.True(headerResult.Success);
        Assert.NotNull(headerResult.Header);

        CsoIndexReader indexReader = new();
        using MemoryStream indexStream = new(bytes);
        CsoIndexReadResult indexResult = indexReader.Read(indexStream, headerResult.Header);

        Assert.False(indexResult.Success);
        Assert.Equal("IndexTableTruncated", indexResult.ErrorCode);
    }

    private static byte[] CreateHeaderOnly(
        ulong uncompressedSize,
        uint blockSize,
        byte version,
        byte indexShift,
        uint headerSize = CsoConstants.MinimumHeaderSize)
    {
        byte[] bytes = new byte[CsoConstants.MinimumHeaderSize];
        WriteHeader(bytes, uncompressedSize, blockSize, version, indexShift, headerSize);
        return bytes;
    }

    private static byte[] CreateCsoLikeFile(
        ulong uncompressedSize,
        uint blockSize,
        byte version,
        byte indexShift,
        uint[] rawEntries,
        int dataLength,
        uint headerSize = CsoConstants.MinimumHeaderSize)
    {
        int indexSize = rawEntries.Length * sizeof(uint);
        int totalLength = Math.Max(CsoConstants.MinimumHeaderSize + indexSize, dataLength);
        byte[] bytes = new byte[totalLength];

        WriteHeader(bytes, uncompressedSize, blockSize, version, indexShift, headerSize);

        int offset = CsoConstants.MinimumHeaderSize;

        foreach (uint rawEntry in rawEntries)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset, sizeof(uint)), rawEntry);
            offset += sizeof(uint);
        }

        return bytes;
    }

    private static void WriteHeader(
        byte[] bytes,
        ulong uncompressedSize,
        uint blockSize,
        byte version,
        byte indexShift,
        uint headerSize = CsoConstants.MinimumHeaderSize)
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