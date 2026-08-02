namespace CsoKit.Application;

public sealed record CsoOperationResult(
    bool Success,
    string Status,
    IReadOnlyList<CsoOperationDetail> DetailLines,
    string? ErrorCode = null,
    string? InputPath = null,
    string? OutputPath = null,
    string? Format = null,
    long? OriginalBytes = null,
    long? ResultBytes = null,
    ICsoOperationData? Data = null)
{
    public string Details => CsoOperationDetailFormatter.Format(DetailLines);

    public static CsoOperationResult Ok(
        string status,
        IReadOnlyList<CsoOperationDetail> detailLines,
        string? inputPath = null,
        string? outputPath = null,
        string? format = null,
        long? originalBytes = null,
        long? resultBytes = null,
        ICsoOperationData? data = null)
    {
        return new CsoOperationResult(
            true,
            status,
            Freeze(detailLines),
            null,
            inputPath,
            outputPath,
            format,
            originalBytes,
            resultBytes,
            data);
    }

    public static CsoOperationResult Fail(
        string status,
        IReadOnlyList<CsoOperationDetail> detailLines,
        string? errorCode = null,
        string? inputPath = null,
        string? outputPath = null,
        string? format = null,
        long? originalBytes = null,
        long? resultBytes = null,
        ICsoOperationData? data = null)
    {
        return new CsoOperationResult(
            false,
            status,
            Freeze(detailLines),
            errorCode,
            inputPath,
            outputPath,
            format,
            originalBytes,
            resultBytes,
            data);
    }

    private static IReadOnlyList<CsoOperationDetail> Freeze(IReadOnlyList<CsoOperationDetail> detailLines)
    {
        ArgumentNullException.ThrowIfNull(detailLines);
        return detailLines.Count == 0 ? Array.Empty<CsoOperationDetail>() : detailLines.ToArray();
    }
}
