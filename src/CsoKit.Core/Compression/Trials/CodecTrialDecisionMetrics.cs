namespace CsoKit.Core.Compression.Trials;

public sealed record CodecTrialDecisionMetrics(
    int CompressedBytes,
    double Ratio,
    double RatioGain,
    double EncodeMilliseconds,
    double DecodeMilliseconds,
    bool PassedRoundtrip,
    bool NativeRequired,
    string CompatibilityRisk);
