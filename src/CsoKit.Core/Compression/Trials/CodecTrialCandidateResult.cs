namespace CsoKit.Core.Compression.Trials;

public sealed record CodecTrialCandidateResult(
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
