using System.Buffers.Binary;
using CsoKit.Core.Formats.Cso;

namespace CsoKit.Tests.Formats.Cso;

public sealed class CsoHeaderReaderTests
{
    [Fact]
    public void Read_WithValidCsoV1Header_ReturnsHeader()
    {
        byte[] bytes = CreateHeader(
            headerSize: 24,
            uncompressedSize: 734003200,
            blockSize: 2048,
            version: 1,
            indexShift: 0);

        CsoHeaderReader reader = new();

        using MemoryStream stream = new(bytes);
        CsoHeaderReadResult result = reader.Read(stream);

        Assert.True(result.Success);
        Assert.NotNull(result.Header);
        Assert.Equal((uint)24, result.Header.HeaderSize);
        Assert.Equal((ulong)734003200, result.Header.UncompressedSize);
        Assert.Equal((uint)2048, result.Header.BlockSize);
        Assert.Equal((byte)1, result.Header.Version);
        Assert.Equal((byte)0, result.Header.IndexShift);
        Assert.Equal(358400, result.Header.SectorCount);
    }

    [Fact]
    public void Read_WithLegacyCsoV1VersionZero_ReturnsHeader()
    {
        byte[] bytes = CreateHeader(
            headerSize: 24,
            uncompressedSize: 4096,
            blockSize: 2048,
            version: 0,
            indexShift: 0);

        CsoHeaderReader reader = new();

        using MemoryStream stream = new(bytes);
        CsoHeaderReadResult result = reader.Read(stream);

        Assert.True(result.Success);
        Assert.NotNull(result.Header);
        Assert.Equal((byte)0, result.Header.Version);
        Assert.True(result.Header.IsCsoV1);
    }

    [Fact]
    public void Read_WithUnreliableCsoV1HeaderSize_UsesMinimumEffectiveHeaderSize()
    {
        byte[] bytes = CreateHeader(
            headerSize: 0,
            uncompressedSize: 4096,
            blockSize: 2048,
            version: 1,
            indexShift: 0);

        CsoHeaderReader reader = new();

        using MemoryStream stream = new(bytes);
        CsoHeaderReadResult result = reader.Read(stream);

        Assert.True(result.Success);
        Assert.NotNull(result.Header);
        Assert.Equal((uint)0, result.Header.HeaderSize);
        Assert.Equal((uint)CsoConstants.MinimumHeaderSize, result.Header.EffectiveHeaderSize);
    }

    [Fact]
    public void Read_WithInvalidCsoV2HeaderSize_ReturnsInvalidHeaderSize()
    {
        byte[] bytes = CreateHeader(
            headerSize: 0,
            uncompressedSize: 4096,
            blockSize: 2048,
            version: 2,
            indexShift: 0);

        CsoHeaderReader reader = new();

        using MemoryStream stream = new(bytes);
        CsoHeaderReadResult result = reader.Read(stream);

        Assert.False(result.Success);
        Assert.Equal("InvalidHeaderSize", result.ErrorCode);
    }

    [Fact]
    public void Read_WithInvalidMagic_ReturnsInvalidMagic()
    {
        byte[] bytes = CreateHeader(
            headerSize: 24,
            uncompressedSize: 1,
            blockSize: 2048,
            version: 1,
            indexShift: 0);

        bytes[0] = (byte)'B';

        CsoHeaderReader reader = new();

        using MemoryStream stream = new(bytes);
        CsoHeaderReadResult result = reader.Read(stream);

        Assert.False(result.Success);
        Assert.Equal("InvalidMagic", result.ErrorCode);
    }

    [Fact]
    public void Read_WithUnsupportedVersion_ReturnsUnsupportedVersion()
    {
        byte[] bytes = CreateHeader(
            headerSize: 24,
            uncompressedSize: 1,
            blockSize: 2048,
            version: 9,
            indexShift: 0);

        CsoHeaderReader reader = new();

        using MemoryStream stream = new(bytes);
        CsoHeaderReadResult result = reader.Read(stream);

        Assert.False(result.Success);
        Assert.Equal("UnsupportedVersion", result.ErrorCode);
    }

    [Fact]
    public void Read_WithTooSmallHeader_ReturnsHeaderTooSmall()
    {
        byte[] bytes = new byte[8];

        CsoHeaderReader reader = new();

        using MemoryStream stream = new(bytes);
        CsoHeaderReadResult result = reader.Read(stream);

        Assert.False(result.Success);
        Assert.Equal("HeaderTooSmall", result.ErrorCode);
    }

    private static byte[] CreateHeader(
        uint headerSize,
        ulong uncompressedSize,
        uint blockSize,
        byte version,
        byte indexShift)
    {
        byte[] bytes = new byte[CsoConstants.MinimumHeaderSize];

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

        return bytes;
    }
}