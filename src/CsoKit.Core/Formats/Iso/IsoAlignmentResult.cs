namespace CsoKit.Core.Formats.Iso;

public sealed record IsoAlignmentResult(
    bool Success,
    string? ErrorCode,
    string? ErrorMessage,
    long PaddingBytes)
{
    public static IsoAlignmentResult Ok(long paddingBytes)
    {
        return new IsoAlignmentResult(true, null, null, paddingBytes);
    }

    public static IsoAlignmentResult Fail(string code, string message)
    {
        return new IsoAlignmentResult(false, code, message, 0);
    }
}
