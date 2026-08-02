namespace CsoKit.Core.Formats.Cso;

public sealed record CsoHeaderReadResult(
    bool Success,
    CsoHeader? Header,
    string? ErrorCode,
    string? ErrorMessage)
{
    public static CsoHeaderReadResult Ok(CsoHeader header)
    {
        return new CsoHeaderReadResult(true, header, null, null);
    }

    public static CsoHeaderReadResult Fail(string errorCode, string errorMessage)
    {
        return new CsoHeaderReadResult(false, null, errorCode, errorMessage);
    }
}