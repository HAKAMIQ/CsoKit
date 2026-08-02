namespace CsoKit.Core.Formats.Cso;

public sealed record CsoOutputSafetyResult(
    bool Success,
    string? ErrorCode,
    string? ErrorMessage)
{
    public static CsoOutputSafetyResult Ok()
    {
        return new CsoOutputSafetyResult(true, null, null);
    }

    public static CsoOutputSafetyResult Fail(string errorCode, string errorMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);

        return new CsoOutputSafetyResult(false, errorCode, errorMessage);
    }
}