namespace CsoKit.Core.Formats.Cso;

public sealed record CsoDiskSpacePreflightResult(
    bool Success,
    string? ErrorCode,
    string? ErrorMessage,
    ulong RequiredBytes,
    ulong AvailableBytes)
{
    public static CsoDiskSpacePreflightResult Ok(
        ulong requiredBytes,
        ulong availableBytes)
    {
        return new CsoDiskSpacePreflightResult(
            true,
            null,
            null,
            requiredBytes,
            availableBytes);
    }

    public static CsoDiskSpacePreflightResult Fail(
        string errorCode,
        string errorMessage,
        ulong requiredBytes = 0,
        ulong availableBytes = 0)
    {
        return new CsoDiskSpacePreflightResult(
            false,
            errorCode,
            errorMessage,
            requiredBytes,
            availableBytes);
    }
}
