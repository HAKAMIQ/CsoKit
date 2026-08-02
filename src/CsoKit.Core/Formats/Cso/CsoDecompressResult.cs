namespace CsoKit.Core.Formats.Cso;

public sealed record CsoDecompressResult(
    bool Success,
    string? ErrorCode,
    string? ErrorMessage,
    ulong BytesWritten)
{
    public static CsoDecompressResult Ok(ulong bytesWritten)
    {
        return new CsoDecompressResult(true, null, null, bytesWritten);
    }

    public static CsoDecompressResult Fail(string errorCode, string errorMessage, ulong bytesWritten = 0)
    {
        return new CsoDecompressResult(false, errorCode, errorMessage, bytesWritten);
    }
}