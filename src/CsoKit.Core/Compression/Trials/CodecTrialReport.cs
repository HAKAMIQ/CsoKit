namespace CsoKit.Core.Compression.Trials;

public sealed record CodecTrialReport(
    int BlockIndex,
    int SourceBytes,
    IReadOnlyList<CodecTrialCandidateResult> Candidates,
    string SelectedCodec,
    bool StoredFallback);
