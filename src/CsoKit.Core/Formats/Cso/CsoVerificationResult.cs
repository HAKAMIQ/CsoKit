namespace CsoKit.Core.Formats.Cso;

public sealed record CsoVerificationResult(
    bool Success,
    CsoHeader? Header,
    IReadOnlyList<CsoIndexEntry> Entries,
    IReadOnlyList<CsoVerificationIssue> Issues)
{
    public static CsoVerificationResult Ok(
        CsoHeader header,
        IReadOnlyList<CsoIndexEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(entries);

        return new CsoVerificationResult(true, header, entries, []);
    }

    public static CsoVerificationResult Fail(
        CsoHeader? header,
        IReadOnlyList<CsoIndexEntry> entries,
        IReadOnlyList<CsoVerificationIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(issues);

        return new CsoVerificationResult(false, header, entries, issues);
    }
}