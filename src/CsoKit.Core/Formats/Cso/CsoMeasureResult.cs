namespace CsoKit.Core.Formats.Cso;

public sealed record CsoMeasureResult(
    bool Success,
    string? ErrorCode,
    string? ErrorMessage,
    ulong OriginalBytes,
    ulong EstimatedBytes,
    ulong EstimatedSavedBytes,
    ulong EstimatedGrowthBytes,
    double EstimatedRatio,
    int TotalBlocks,
    int CompressedBlocks,
    int StoredBlocks)
{
    public static CsoMeasureResult Ok(
        ulong originalBytes,
        ulong estimatedBytes,
        int totalBlocks,
        int compressedBlocks,
        int storedBlocks)
    {
        ValidateBlockCounts(totalBlocks, compressedBlocks, storedBlocks);

        if (compressedBlocks + storedBlocks != totalBlocks)
        {
            throw new ArgumentOutOfRangeException(
                nameof(storedBlocks),
                "Compressed and stored block counts must equal the total block count.");
        }

        return Create(
            success: true,
            errorCode: null,
            errorMessage: null,
            originalBytes,
            estimatedBytes,
            totalBlocks,
            compressedBlocks,
            storedBlocks);
    }

    public static CsoMeasureResult Fail(
        string errorCode,
        string errorMessage,
        ulong originalBytes = 0,
        ulong estimatedBytes = 0,
        int totalBlocks = 0,
        int compressedBlocks = 0,
        int storedBlocks = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);
        ValidateBlockCounts(totalBlocks, compressedBlocks, storedBlocks);

        return Create(
            success: false,
            errorCode,
            errorMessage,
            originalBytes,
            estimatedBytes,
            totalBlocks,
            compressedBlocks,
            storedBlocks);
    }

    private static CsoMeasureResult Create(
        bool success,
        string? errorCode,
        string? errorMessage,
        ulong originalBytes,
        ulong estimatedBytes,
        int totalBlocks,
        int compressedBlocks,
        int storedBlocks)
    {
        ulong savedBytes = originalBytes > estimatedBytes
            ? originalBytes - estimatedBytes
            : 0;

        ulong growthBytes = estimatedBytes > originalBytes
            ? estimatedBytes - originalBytes
            : 0;

        double ratio = originalBytes == 0
            ? 0
            : (double)estimatedBytes / originalBytes;

        return new CsoMeasureResult(
            success,
            errorCode,
            errorMessage,
            originalBytes,
            estimatedBytes,
            savedBytes,
            growthBytes,
            ratio,
            totalBlocks,
            compressedBlocks,
            storedBlocks);
    }

    private static void ValidateBlockCounts(
        int totalBlocks,
        int compressedBlocks,
        int storedBlocks)
    {
        if (totalBlocks < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalBlocks), "Total block count cannot be negative.");
        }

        if (compressedBlocks < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(compressedBlocks), "Compressed block count cannot be negative.");
        }

        if (storedBlocks < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(storedBlocks), "Stored block count cannot be negative.");
        }

        if (compressedBlocks > totalBlocks)
        {
            throw new ArgumentOutOfRangeException(nameof(compressedBlocks), "Compressed block count cannot exceed total block count.");
        }

        if (storedBlocks > totalBlocks)
        {
            throw new ArgumentOutOfRangeException(nameof(storedBlocks), "Stored block count cannot exceed total block count.");
        }
    }
}