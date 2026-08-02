using CsoKit.Core.Compression.Trials;

namespace CsoKit.Core.Formats.Cso;

public sealed record CsoCompressResult(
    bool Success,
    string? ErrorCode,
    string? ErrorMessage,
    ulong BytesRead,
    ulong BytesWritten,
    int CompressedBlocks,
    int StoredBlocks,
    IReadOnlyDictionary<string, int>? CodecWins = null,
    CodecTrialSummary? CodecTrialSummary = null,
    int ZeroBlocks = 0)
{
    public IReadOnlyDictionary<string, int> EffectiveCodecWins => CodecWins ?? EmptyCodecWins;

    private static readonly IReadOnlyDictionary<string, int> EmptyCodecWins =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    public static CsoCompressResult Ok(
        ulong bytesRead,
        ulong bytesWritten,
        int compressedBlocks,
        int storedBlocks,
        IReadOnlyDictionary<string, int>? codecWins = null,
        CodecTrialSummary? codecTrialSummary = null,
        int zeroBlocks = 0)
    {
        return new CsoCompressResult(
            true,
            null,
            null,
            bytesRead,
            bytesWritten,
            compressedBlocks,
            storedBlocks,
            codecWins,
            codecTrialSummary,
            zeroBlocks);
    }

    public static CsoCompressResult Fail(
        string errorCode,
        string errorMessage,
        ulong bytesRead = 0,
        ulong bytesWritten = 0,
        int compressedBlocks = 0,
        int storedBlocks = 0,
        IReadOnlyDictionary<string, int>? codecWins = null,
        CodecTrialSummary? codecTrialSummary = null,
        int zeroBlocks = 0)
    {
        return new CsoCompressResult(
            false,
            errorCode,
            errorMessage,
            bytesRead,
            bytesWritten,
            compressedBlocks,
            storedBlocks,
            codecWins,
            codecTrialSummary,
            zeroBlocks);
    }
}
