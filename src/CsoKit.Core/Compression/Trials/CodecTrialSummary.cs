namespace CsoKit.Core.Compression.Trials;

public sealed record CodecTrialSummary(
    int BlocksReported,
    IReadOnlyList<CodecTrialReport> Blocks,
    IReadOnlyDictionary<string, int> SelectedCodecWins,
    IReadOnlyDictionary<string, int> RejectedReasons,
    IReadOnlyDictionary<string, int> CandidateAttempts);
