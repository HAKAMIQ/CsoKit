namespace CsoKit.Core.Formats.Cso.Codecs;

public sealed record CsoTrialPlan(
    CsoCompressionProfile Profile,
    bool UseZopfli,
    bool UseExperimental,
    IReadOnlyList<CsoCodecTrial> Trials);
