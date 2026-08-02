namespace CsoKit.Core.Formats.Iso;

public sealed record PspIsoValidationResult(
    bool Success,
    string InputPath,
    long InputBytes,
    long PaddingBytes,
    bool HasIso9660PrimaryVolumeDescriptor,
    bool HasUmdDataBin,
    bool HasParamSfo,
    bool HasEbootBin,
    IReadOnlyList<PspIsoValidationIssue> Issues,
    IReadOnlyList<string> Warnings,
    bool HasPspGame = false,
    string? DiscIdFromUmdData = null,
    string? DiscIdFromParamSfo = null,
    string? Title = null,
    string? Category = null,
    string? PspSystemVersion = null)
{
    public static PspIsoValidationResult Fail(
        string inputPath,
        long inputBytes,
        IReadOnlyList<PspIsoValidationIssue> issues,
        IReadOnlyList<string>? warnings = null,
        long paddingBytes = 0,
        bool hasIso9660PrimaryVolumeDescriptor = false,
        bool hasUmdDataBin = false,
        bool hasParamSfo = false,
        bool hasEbootBin = false,
        bool hasPspGame = false,
        string? discIdFromUmdData = null,
        string? discIdFromParamSfo = null,
        string? title = null,
        string? category = null,
        string? pspSystemVersion = null)
    {
        return new PspIsoValidationResult(
            false,
            inputPath,
            inputBytes,
            paddingBytes,
            hasIso9660PrimaryVolumeDescriptor,
            hasUmdDataBin,
            hasParamSfo,
            hasEbootBin,
            issues,
            warnings ?? [],
            hasPspGame,
            discIdFromUmdData,
            discIdFromParamSfo,
            title,
            category,
            pspSystemVersion);
    }
}