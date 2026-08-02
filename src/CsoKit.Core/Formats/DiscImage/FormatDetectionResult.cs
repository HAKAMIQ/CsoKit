namespace CsoKit.Core.Formats.DiscImage;

public sealed record FormatDetectionResult(
    bool Success,
    DetectedDiscFormat Format,
    string Magic,
    uint? HeaderSize,
    ulong? UncompressedSize,
    uint? BlockSize,
    byte? IndexShift,
    long? SectorCount,
    IReadOnlyList<string> Warnings,
    string? ErrorCode = null,
    string? ErrorMessage = null)
{
    public static FormatDetectionResult Fail(string code, string message)
    {
        return new FormatDetectionResult(
            false,
            DetectedDiscFormat.Unknown,
            string.Empty,
            null,
            null,
            null,
            null,
            null,
            [],
            code,
            message);
    }
}