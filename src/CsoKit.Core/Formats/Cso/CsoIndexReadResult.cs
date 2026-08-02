namespace CsoKit.Core.Formats.Cso;

public sealed record CsoIndexReadResult(
    bool Success,
    IReadOnlyList<CsoIndexEntry> Entries,
    string? ErrorCode,
    string? ErrorMessage)
{
    public static CsoIndexReadResult Ok(IReadOnlyList<CsoIndexEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        return new CsoIndexReadResult(true, entries, null, null);
    }

    public static CsoIndexReadResult Fail(string errorCode, string errorMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);

        return new CsoIndexReadResult(false, [], errorCode, errorMessage);
    }
}