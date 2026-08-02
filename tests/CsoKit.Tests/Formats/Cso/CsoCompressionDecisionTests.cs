using CsoKit.Core.Formats.Cso;

namespace CsoKit.Tests.Formats.Cso;

public sealed class CsoCompressionDecisionTests
{
    [Fact]
    public void BestCandidateSelector_Select_WhenCompressedCandidateIsSmaller_ReturnsCompressedCandidate()
    {
        byte[] source = new byte[16];
        SectorJob job = new(
            BlockIndex: 3,
            SourceOffset: 4096,
            SourceLength: source.Length,
            SourceBuffer: source);

        SectorResult candidate = new(
            job.BlockIndex,
            job.SourceOffset,
            job.SourceLength,
            OutputLength: 4,
            IsStored: false,
            Method: CompressionMethod.RawDeflate,
            Level: 9,
            Buffer: [1, 2, 3, 4]);

        byte[] expected = [1, 2, 3, 4];
        SectorResult result = CsoBestCandidateSelector.Select(job, candidate);

        Assert.False(result.IsStored);
        Assert.Equal(CompressionMethod.RawDeflate, result.Method);
        Assert.Equal(9, result.Level);
        Assert.Equal(expected, result.OutputSpan.ToArray());
    }

    [Fact]
    public void BestCandidateSelector_Select_WhenCompressedCandidateIsEqualSize_ReturnsStoredSector()
    {
        byte[] source = [10, 20, 30, 40];
        SectorJob job = new(
            BlockIndex: 1,
            SourceOffset: 2048,
            SourceLength: source.Length,
            SourceBuffer: source);

        SectorResult candidate = new(
            job.BlockIndex,
            job.SourceOffset,
            job.SourceLength,
            OutputLength: source.Length,
            IsStored: false,
            Method: CompressionMethod.RawDeflate,
            Level: 9,
            Buffer: [1, 2, 3, 4]);

        SectorResult result = CsoBestCandidateSelector.Select(job, candidate);

        Assert.True(result.IsStored);
        Assert.Equal(CompressionMethod.Store, result.Method);
        Assert.Equal(0, result.Level);
        Assert.Equal(source, result.OutputSpan.ToArray());
    }

    [Fact]
    public void BestCandidateSelector_Select_WhenCompressedCandidateIsLarger_ReturnsStoredSector()
    {
        byte[] source = [1, 2, 3];
        SectorJob job = new(
            BlockIndex: 2,
            SourceOffset: 4096,
            SourceLength: source.Length,
            SourceBuffer: source);

        SectorResult candidate = new(
            job.BlockIndex,
            job.SourceOffset,
            job.SourceLength,
            OutputLength: 5,
            IsStored: false,
            Method: CompressionMethod.RawDeflate,
            Level: 9,
            Buffer: [1, 2, 3, 4, 5]);

        SectorResult result = CsoBestCandidateSelector.Select(job, candidate);

        Assert.True(result.IsStored);
        Assert.Equal(CompressionMethod.Store, result.Method);
        Assert.Equal(source, result.OutputSpan.ToArray());
    }

    [Fact]
    public void BestCandidateSelector_Select_WhenMultipleCandidatesExist_ReturnsSmallestCandidate()
    {
        byte[] source = new byte[32];
        SectorJob job = new(
            BlockIndex: 5,
            SourceOffset: 10240,
            SourceLength: source.Length,
            SourceBuffer: source);

        SectorResult larger = new(
            job.BlockIndex,
            job.SourceOffset,
            job.SourceLength,
            OutputLength: 12,
            IsStored: false,
            Method: CompressionMethod.RawDeflate,
            Level: 6,
            Buffer: new byte[12]);

        SectorResult smaller = new(
            job.BlockIndex,
            job.SourceOffset,
            job.SourceLength,
            OutputLength: 8,
            IsStored: false,
            Method: CompressionMethod.RawDeflate,
            Level: 9,
            Buffer: new byte[8]);

        CsoBestCandidateSelector selector = new();
        SectorResult[] candidates = [larger, smaller];
        SectorResult result = selector.Select(job, candidates);

        Assert.False(result.IsStored);
        Assert.Equal(8, result.OutputLength);
        Assert.Equal(9, result.Level);
    }

    [Fact]
    public void BestCandidateSelector_FastProfile_WhenNearTieCandidateIsMuchCheaper_PrefersFasterCandidate()
    {
        byte[] source = new byte[2048];
        SectorJob job = new(
            BlockIndex: 6,
            SourceOffset: 12288,
            SourceLength: source.Length,
            SourceBuffer: source);

        SectorResult slowSmaller = CreateDecisionCandidate(
            job,
            outputLength: 100,
            codecName: "slow-smaller",
            encodeMilliseconds: 100,
            decodeMilliseconds: 10);

        SectorResult fastLarger = CreateDecisionCandidate(
            job,
            outputLength: 105,
            codecName: "fast-larger",
            encodeMilliseconds: 1,
            decodeMilliseconds: 1);

        CsoBestCandidateSelector selector = new(CsoCompressionProfile.Fast);
        SectorResult[] candidates = [slowSmaller, fastLarger];
        SectorResult result = selector.Select(job, candidates);

        Assert.Equal("fast-larger", result.EffectiveCodecName);
    }

    [Fact]
    public void BestCandidateSelector_GameSafeProfile_WhenNearTieCandidateIsMuchCheaper_StillPrefersSmallest()
    {
        byte[] source = new byte[2048];
        SectorJob job = new(
            BlockIndex: 7,
            SourceOffset: 14336,
            SourceLength: source.Length,
            SourceBuffer: source);

        SectorResult slowSmaller = CreateDecisionCandidate(
            job,
            outputLength: 100,
            codecName: "slow-smaller",
            encodeMilliseconds: 100,
            decodeMilliseconds: 10);

        SectorResult fastLarger = CreateDecisionCandidate(
            job,
            outputLength: 105,
            codecName: "fast-larger",
            encodeMilliseconds: 1,
            decodeMilliseconds: 1);

        CsoBestCandidateSelector selector = new(CsoCompressionProfile.GameSafe);
        SectorResult[] candidates = [slowSmaller, fastLarger];
        SectorResult result = selector.Select(job, candidates);

        Assert.Equal("slow-smaller", result.EffectiveCodecName);
    }

    [Fact]
    public void CompressionWorker_Compress_WhenBlockIsSmallAndUnhelpful_StoresOriginalBytes()
    {
        byte[] source = [1, 2, 3, 4];
        SectorJob job = new(
            BlockIndex: 0,
            SourceOffset: 0,
            SourceLength: source.Length,
            SourceBuffer: source);

        CsoCompressionWorker worker = new();
        SectorResult result = worker.Compress(job);

        Assert.True(result.IsStored);
        Assert.Equal(CompressionMethod.Store, result.Method);
        Assert.Equal(source, result.OutputSpan.ToArray());
    }

    [Fact]
    public void CompressionWorker_Compress_WhenBlockIsCompressible_ReturnsCompressedCandidate()
    {
        byte[] source = new byte[2048];
        SectorJob job = new(
            BlockIndex: 0,
            SourceOffset: 0,
            SourceLength: source.Length,
            SourceBuffer: source);

        CsoCompressionWorker worker = new();
        SectorResult result = worker.Compress(job);

        Assert.False(result.IsStored);
        Assert.Equal(CompressionMethod.RawDeflate, result.Method);
        Assert.NotEqual("store", result.EffectiveCodecName);
        Assert.True(result.OutputLength < source.Length);
    }

    private static SectorResult CreateDecisionCandidate(
        SectorJob job,
        int outputLength,
        string codecName,
        double encodeMilliseconds,
        double decodeMilliseconds)
    {
        return new SectorResult(
            job.BlockIndex,
            job.SourceOffset,
            job.SourceLength,
            outputLength,
            IsStored: false,
            Method: CompressionMethod.RawDeflate,
            Level: 6,
            Buffer: new byte[outputLength],
            CodecName: codecName,
            DecisionMetrics: new(
                outputLength,
                Ratio: (double)outputLength / job.SourceLength,
                RatioGain: 1.0 - ((double)outputLength / job.SourceLength),
                encodeMilliseconds,
                decodeMilliseconds,
                PassedRoundtrip: true,
                NativeRequired: false,
                CompatibilityRisk: "standard-raw-deflate"));
    }
}

public sealed class CsoCompressionProfileWorkerTests
{
    private static readonly string[] FastProfileCodecNames =
    [
        "managed-deflate-fastest",
        "native-libdeflate-1",
        "native-libdeflate-6",
    ];

    private static readonly string[] SmallestProfileCodecNames =
    [
        "managed-deflate-fastest",
        "managed-deflate-optimal",
        "managed-deflate-smallest",
        "native-zlib-default",
        "native-zlib-filtered",
        "native-zlib-huffman-only",
        "native-zlib-rle",
        "native-libdeflate-6",
        "native-libdeflate-9",
        "native-libdeflate-12",
    ];

    [Fact]
    public void CompressionWorker_WithFastProfile_ReportsFastLogicalLevel()
    {
        byte[] source = new byte[2048];
        SectorJob job = new(
            BlockIndex: 0,
            SourceOffset: 0,
            SourceLength: source.Length,
            SourceBuffer: source);

        CsoCompressionWorker worker = new(CsoCompressionProfile.Fast);
        SectorResult result = worker.Compress(job);

        Assert.False(result.IsStored);
        Assert.Equal(CompressionMethod.RawDeflate, result.Method);
        Assert.Contains(result.EffectiveCodecName, FastProfileCodecNames);
    }

    [Fact]
    public void CompressionWorker_WithSmallestProfile_ReportsSmallestLogicalLevel()
    {
        byte[] source = new byte[2048];
        SectorJob job = new(
            BlockIndex: 0,
            SourceOffset: 0,
            SourceLength: source.Length,
            SourceBuffer: source);

        CsoCompressionWorker worker = new(CsoCompressionProfile.Smallest);
        SectorResult result = worker.Compress(job);

        Assert.False(result.IsStored);
        Assert.Equal(CompressionMethod.RawDeflate, result.Method);
        Assert.Contains(result.EffectiveCodecName, SmallestProfileCodecNames);
    }
}