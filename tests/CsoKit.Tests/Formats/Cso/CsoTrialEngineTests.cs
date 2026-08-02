using System.IO.Compression;
using CsoKit.Core.Formats.Cso;
using CsoKit.Core.Formats.Cso.Codecs;

namespace CsoKit.Tests.Formats.Cso;

public sealed class CsoTrialEngineTests
{
    [Fact]
    public void Compress_SelectsSmallestValidCandidate()
    {
        byte[] source = new byte[2048];
        Array.Fill(source, (byte)0x41);

        byte[] small = CompressRawDeflate(source);
        byte[] large = new byte[small.Length + 4];
        small.CopyTo(large, 0);

        SectorJob job = new(0, 0, source.Length, source);

        CsoTrialEngine engine = new(
            [
                new FixedTrial(CsoCodecKind.ManagedDeflateFastest, "valid-large", large),
                new FixedTrial(CsoCodecKind.ManagedDeflateSmallest, "valid-small", small),
            ],
            new CsoBestCandidateSelector());

        SectorResult result = engine.Compress(job);

        Assert.False(result.IsStored);
        Assert.Equal("valid-small", result.EffectiveCodecName);
        Assert.Equal(small.Length, result.OutputLength);
    }

    [Fact]
    public void Compress_RejectsCandidateThatDoesNotRoundtrip()
    {
        byte[] source = new byte[2048];
        Array.Fill(source, (byte)0x5A);

        SectorJob job = new(0, 0, source.Length, source);

        CsoTrialEngine engine = new(
            [new FixedTrial(CsoCodecKind.ManagedDeflateFastest, "bad-candidate", [1, 2, 3, 4])],
            new CsoBestCandidateSelector());

        SectorResult result = engine.Compress(job);

        Assert.True(result.IsStored);
        Assert.Equal("store", result.EffectiveCodecName);
    }

    private static byte[] CompressRawDeflate(byte[] block)
    {
        using MemoryStream compressed = new();

        using (DeflateStream deflate = new(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            deflate.Write(block);
        }

        return compressed.ToArray();
    }

    private sealed class FixedTrial(
        CsoCodecKind kind,
        string name,
        byte[] output) : ICsoCodecTrial
    {
        public CsoCodecKind Kind { get; } = kind;

        public string Name { get; } = name;

        public bool TryCompressRawDeflate(
            ReadOnlySpan<byte> input,
            out CsoCodecTrialResult result)
        {
            result = new CsoCodecTrialResult(Kind, Name, output, output.Length, Success: true);
            return true;
        }
    }
}