using CsoKit.Core.Formats.Psp;

namespace CsoKit.Core.Analysis;

public sealed record PspIsoAnalysisResult(
    bool HasPspGame,
    bool HasUmdData,
    bool HasParamSfo,
    string? DiscIdFromUmdData,
    string? DiscIdFromParamSfo,
    string? Title,
    string? Category,
    string? PspSystemVersion,
    IReadOnlyList<string> Warnings)
{
    public PspDiscIdentity ParamSfoIdentity => new(
        Title,
        DiscIdFromParamSfo,
        Category,
        PspSystemVersion);
}
