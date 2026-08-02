using CsoKit.Core.Formats.Cso;

namespace CsoKit.Tests.Formats.Cso;

public sealed class CsoSectorEngineFoundationTests
{
    [Fact]
    public void CsoIndexBuilder_AddSectorOffset_WhenStored_SetsHighBit()
    {
        CsoIndexBuilder builder = new(sectorCount: 1);

        builder.AddSectorOffset(0x1234, isStored: true);
        builder.AddFinalOffset(0x2345);

        Assert.Equal(2, builder.Entries.Count);
        Assert.Equal(0x80001234u, builder.Entries[0]);
        Assert.Equal(0x00002345u, builder.Entries[1]);
    }

    [Fact]
    public void CsoIndexBuilder_AddSectorOffset_WhenCompressed_DoesNotSetHighBit()
    {
        CsoIndexBuilder builder = new(sectorCount: 1);

        builder.AddSectorOffset(0x1234, isStored: false);

        Assert.Single(builder.Entries);
        Assert.Equal(0x00001234u, builder.Entries[0]);
    }

    [Fact]
    public void CsoBlockReader_ReadExactlyOrLess_ReadsRequestedBytes()
    {
        byte[] source = [1, 2, 3, 4, 5];
        byte[] destination = new byte[3];

        using MemoryStream stream = new(source);
        int read = CsoBlockReader.ReadExactlyOrLess(stream, destination);

        Assert.Equal(3, read);
        Assert.Equal([1, 2, 3], destination);
    }

    [Fact]
    public void SectorJob_StoresBlockIdentityAndSourceRange()
    {
        byte[] source = [10, 20, 30, 40];
        SectorJob job = new(
            BlockIndex: 7,
            SourceOffset: 2048,
            SourceLength: 3,
            SourceBuffer: source);

        Assert.Equal(7, job.BlockIndex);
        Assert.Equal(2048UL, job.SourceOffset);
        Assert.Equal(3, job.SourceLength);
        Assert.Equal([10, 20, 30], job.SourceSpan.ToArray());
    }

    [Fact]
    public void SectorResult_ExposesOutputSpanForWrittenBytesOnly()
    {
        SectorResult result = new(
            BlockIndex: 2,
            SourceOffset: 4096,
            SourceLength: 4,
            OutputLength: 2,
            IsStored: false,
            Method: CompressionMethod.RawDeflate,
            Level: 9,
            Buffer: [80, 90, 100]);

        Assert.Equal(2, result.BlockIndex);
        Assert.False(result.IsStored);
        Assert.Equal(CompressionMethod.RawDeflate, result.Method);
        Assert.Equal([80, 90], result.OutputSpan.ToArray());
    }
}