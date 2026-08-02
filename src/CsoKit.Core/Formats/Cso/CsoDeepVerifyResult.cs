namespace CsoKit.Core.Formats.Cso;

public sealed record CsoDeepVerifyResult(
    bool Success,
    CsoHeader? Header,
    int BlocksChecked,
    ulong BytesReconstructed,
    string? Sha256,
    IReadOnlyList<CsoDeepVerifyIssue> Issues)
{
    public string AlgorithmName { get; init; } = "Hybrid CSO verification";

    public string VerificationScope { get; init; } = "Header + index + block payload reconstruction";

    public string LegacyLayer { get; init; } = "Legacy structural CSO header/index validation";

    public string ModernLayer { get; init; } = "Streaming payload decode with pooled compressed buffers";

    public string ForensicLayer { get; init; } = "Coverage, topology, bounds, and reconstruction diagnostics";

    public long? FileLength { get; init; }

    public long? HeaderSize { get; init; }

    public long? IndexEntryCount { get; init; }

    public long? IndexTableBytes { get; init; }

    public long? IndexEndOffset { get; init; }

    public long? FirstDataOffset { get; init; }

    public long? FinalDataOffset { get; init; }

    public long TotalBlocks { get; init; }

    public ulong ExpectedReconstructedBytes { get; init; }

    public ulong PhysicalPayloadBytes { get; init; }

    public int CompressedBlocks { get; init; }

    public int StoredBlocks { get; init; }

    public int ZeroBlocks { get; init; }

    public int PayloadBlocksDecoded { get; init; }

    public bool Sha256Computed => !string.IsNullOrWhiteSpace(Sha256);

    public double CoveragePercent => TotalBlocks <= 0
        ? BlocksChecked == 0 ? 0.0 : 100.0
        : Math.Min(100.0, (double)BlocksChecked / TotalBlocks * 100.0);

    public static CsoDeepVerifyResult Fail(
        CsoHeader? header,
        int blocksChecked,
        ulong bytesReconstructed,
        IReadOnlyList<CsoDeepVerifyIssue> issues)
    {
        return new CsoDeepVerifyResult(
            Success: false,
            header,
            blocksChecked,
            bytesReconstructed,
            Sha256: null,
            issues)
        {
            TotalBlocks = header?.SectorCount ?? 0,
            ExpectedReconstructedBytes = header?.UncompressedSize ?? 0,
        };
    }

    public static CsoDeepVerifyResult Ok(
        CsoHeader? header,
        int blocksChecked,
        ulong bytesReconstructed,
        string? sha256)
    {
        return new CsoDeepVerifyResult(
            Success: true,
            header,
            blocksChecked,
            bytesReconstructed,
            sha256,
            Array.Empty<CsoDeepVerifyIssue>())
        {
            TotalBlocks = header?.SectorCount ?? blocksChecked,
            ExpectedReconstructedBytes = header?.UncompressedSize ?? bytesReconstructed,
            PayloadBlocksDecoded = blocksChecked,
        };
    }
}
