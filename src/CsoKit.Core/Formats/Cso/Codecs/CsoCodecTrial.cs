namespace CsoKit.Core.Formats.Cso.Codecs;

public sealed record CsoCodecTrial(
    CsoCodecKind Kind,
    string Name,
    bool RequiresNative,
    bool IsSlow,
    bool IsExperimental);
