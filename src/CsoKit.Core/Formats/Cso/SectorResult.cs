using CsoKit.Core.Compression.Trials;

namespace CsoKit.Core.Formats.Cso;

public sealed record SectorResult(
    int BlockIndex,
    ulong SourceOffset,
    int SourceLength,
    int OutputLength,
    bool IsStored,
    CompressionMethod Method,
    int Level,
    byte[] Buffer,
    string CodecName = "",
    CodecTrialReport? TrialReport = null,
    CodecTrialDecisionMetrics? DecisionMetrics = null,
    bool SourceIsAllZero = false)
{
    public ReadOnlySpan<byte> OutputSpan => Buffer.AsSpan(0, OutputLength);

    public string EffectiveCodecName => string.IsNullOrWhiteSpace(CodecName)
        ? Method.ToString().ToLowerInvariant()
        : CodecName;
}
