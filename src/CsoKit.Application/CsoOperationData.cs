namespace CsoKit.Application;

public interface ICsoOperationData
{
}

public sealed record CsoMeasureOperationData(
    ulong OriginalBytes,
    ulong EstimatedBytes,
    ulong EstimatedSavedBytes,
    ulong EstimatedGrowthBytes,
    double EstimatedRatio,
    int TotalBlocks,
    int CompressedBlocks,
    int StoredBlocks) : ICsoOperationData;

public sealed record CsoCompressOperationData(
    ulong BytesRead,
    ulong BytesWritten,
    int CompressedBlocks,
    int StoredBlocks,
    int ZeroBlocks,
    IReadOnlyDictionary<string, int> CodecWins,
    CsoCodecTrialSummaryData? CodecReport) : ICsoOperationData;

public sealed record CsoDecompressOperationData(
    ulong BytesWritten,
    string? InputFormat) : ICsoOperationData;

public sealed record CsoRepairOperationData(
    string InputFormat,
    ulong BytesRead,
    ulong BytesWritten,
    long PaddingBytes,
    string Mode,
    bool UsedTempIso,
    string? FallbackReason,
    int CompressedBlocks,
    int StoredBlocks,
    int ZeroBlocks,
    string RepairMode,
    bool CorruptionDetected,
    string InputVerificationStatus,
    string OutputVerificationStatus,
    string ActionTaken,
    string Conclusion,
    IReadOnlyList<CsoOperationIssueData> InputIssues,
    IReadOnlyList<CsoOperationIssueData> OutputIssues,
    CsoCodecTrialSummaryData? CodecReport) : ICsoOperationData;

public sealed record CsoVerifyOperationData(
    bool Deep,
    string? DetectedFormat,
    CsoHeaderData? Header,
    int EntriesRead,
    long? ExpectedEntries,
    int BlocksChecked,
    ulong BytesReconstructed,
    string? Sha256,
    IReadOnlyList<CsoOperationIssueData> Issues) : ICsoOperationData;

public sealed record CsoHeaderData(
    byte Version,
    uint HeaderSize,
    uint EffectiveHeaderSize,
    ulong UncompressedSize,
    uint BlockSize,
    long SectorCount,
    byte IndexShift,
    long IndexEntryCount,
    long IndexTableSizeBytes);

public sealed record CsoOperationIssueData(
    string Code,
    string Message,
    int? BlockIndex = null,
    long? Offset = null,
    string? Expected = null,
    string? Actual = null);

public sealed record CsoCodecTrialSummaryData(
    int BlocksReported,
    IReadOnlyList<CsoCodecTrialReportData> Blocks,
    IReadOnlyDictionary<string, int> SelectedCodecWins,
    IReadOnlyDictionary<string, int> RejectedReasons,
    IReadOnlyDictionary<string, int> CandidateAttempts);

public sealed record CsoCodecTrialReportData(
    int BlockIndex,
    int SourceBytes,
    IReadOnlyList<CsoCodecTrialCandidateData> Candidates,
    string SelectedCodec,
    bool StoredFallback);

public sealed record CsoCodecTrialCandidateData(
    string CodecName,
    string CodecFamily,
    int Level,
    int CompressedBytes,
    double Ratio,
    double EncodeMilliseconds,
    double DecodeMilliseconds,
    bool PassedRoundtrip,
    string? RejectedReason,
    bool SelectedWinner,
    string? FallbackReason);
