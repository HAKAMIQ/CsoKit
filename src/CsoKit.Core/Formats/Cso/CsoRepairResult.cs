using CsoKit.Core.Compression.Trials;

namespace CsoKit.Core.Formats.Cso;

public sealed record CsoRepairResult(
    bool Success,
    string? ErrorCode,
    string? ErrorMessage,
    string InputFormat,
    ulong BytesRead,
    ulong BytesWritten,
    long PaddingBytes,
    string Mode = "temp-iso-fallback",
    bool UsedTempIso = true,
    string? FallbackReason = null,
    CodecTrialSummary? CodecTrialSummary = null,
    int CompressedBlocks = 0,
    int StoredBlocks = 0,
    int ZeroBlocks = 0)
{
    private static readonly IReadOnlyList<CsoDeepVerifyIssue> EmptyIssues = Array.Empty<CsoDeepVerifyIssue>();

    public CsoRepairMode RepairMode { get; init; } = CsoRepairMode.RebuildOnly;

    public bool CorruptionDetected { get; init; }

    public string InputVerificationStatus { get; init; } = "NotRun";

    public string OutputVerificationStatus { get; init; } = "NotRun";

    public string ActionTaken { get; init; } = "Rebuilt a normalized CSO output.";

    public string Conclusion { get; init; } = "No corruption was proven in the input file.";

    public IReadOnlyList<CsoDeepVerifyIssue> InputIssues { get; init; } = EmptyIssues;

    public IReadOnlyList<CsoDeepVerifyIssue> OutputIssues { get; init; } = EmptyIssues;

    public IReadOnlyList<CsoDeepVerifyIssue> EffectiveInputIssues => InputIssues;

    public IReadOnlyList<CsoDeepVerifyIssue> EffectiveOutputIssues => OutputIssues;

    public static CsoRepairResult Ok(
        string inputFormat,
        ulong bytesRead,
        ulong bytesWritten,
        long paddingBytes,
        string mode = "temp-iso-fallback",
        bool usedTempIso = true,
        string? fallbackReason = null,
        CodecTrialSummary? codecTrialSummary = null,
        int compressedBlocks = 0,
        int storedBlocks = 0,
        int zeroBlocks = 0,
        CsoRepairMode repairMode = CsoRepairMode.RebuildOnly,
        bool corruptionDetected = false,
        string inputVerificationStatus = "NotRun",
        string outputVerificationStatus = "NotRun",
        string actionTaken = "Rebuilt a normalized CSO output.",
        string conclusion = "No corruption was proven in the input file.",
        IReadOnlyList<CsoDeepVerifyIssue>? inputIssues = null,
        IReadOnlyList<CsoDeepVerifyIssue>? outputIssues = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputFormat);
        ArgumentException.ThrowIfNullOrWhiteSpace(mode);
        ArgumentException.ThrowIfNullOrWhiteSpace(inputVerificationStatus);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputVerificationStatus);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionTaken);
        ArgumentException.ThrowIfNullOrWhiteSpace(conclusion);
        ValidatePaddingBytes(paddingBytes);
        ValidateBlockCounts(compressedBlocks, storedBlocks, zeroBlocks);

        return new CsoRepairResult(
            true,
            null,
            null,
            inputFormat,
            bytesRead,
            bytesWritten,
            paddingBytes,
            mode,
            usedTempIso,
            fallbackReason,
            codecTrialSummary,
            compressedBlocks,
            storedBlocks,
            zeroBlocks)
        {
            RepairMode = repairMode,
            CorruptionDetected = corruptionDetected,
            InputVerificationStatus = inputVerificationStatus,
            OutputVerificationStatus = outputVerificationStatus,
            ActionTaken = actionTaken,
            Conclusion = conclusion,
            InputIssues = inputIssues ?? EmptyIssues,
            OutputIssues = outputIssues ?? EmptyIssues,
        };
    }

    public static CsoRepairResult Fail(
        string code,
        string message,
        string inputFormat = "Unknown",
        string mode = "temp-iso-fallback",
        bool usedTempIso = true,
        string? fallbackReason = null,
        CodecTrialSummary? codecTrialSummary = null,
        int compressedBlocks = 0,
        int storedBlocks = 0,
        int zeroBlocks = 0,
        CsoRepairMode repairMode = CsoRepairMode.RebuildOnly,
        bool corruptionDetected = false,
        string inputVerificationStatus = "NotRun",
        string outputVerificationStatus = "NotProduced",
        string actionTaken = "No safe repair output was produced.",
        string conclusion = "Repair did not complete.",
        IReadOnlyList<CsoDeepVerifyIssue>? inputIssues = null,
        IReadOnlyList<CsoDeepVerifyIssue>? outputIssues = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(inputFormat);
        ArgumentException.ThrowIfNullOrWhiteSpace(mode);
        ArgumentException.ThrowIfNullOrWhiteSpace(inputVerificationStatus);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputVerificationStatus);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionTaken);
        ArgumentException.ThrowIfNullOrWhiteSpace(conclusion);
        ValidateBlockCounts(compressedBlocks, storedBlocks, zeroBlocks);

        return new CsoRepairResult(
            false,
            code,
            message,
            inputFormat,
            0,
            0,
            0,
            mode,
            usedTempIso,
            fallbackReason,
            codecTrialSummary,
            compressedBlocks,
            storedBlocks,
            zeroBlocks)
        {
            RepairMode = repairMode,
            CorruptionDetected = corruptionDetected,
            InputVerificationStatus = inputVerificationStatus,
            OutputVerificationStatus = outputVerificationStatus,
            ActionTaken = actionTaken,
            Conclusion = conclusion,
            InputIssues = inputIssues ?? EmptyIssues,
            OutputIssues = outputIssues ?? EmptyIssues,
        };
    }

    public CsoRepairResult WithDiagnostics(
        CsoRepairMode repairMode,
        bool corruptionDetected,
        string inputVerificationStatus,
        string outputVerificationStatus,
        string actionTaken,
        string conclusion,
        IReadOnlyList<CsoDeepVerifyIssue>? inputIssues = null,
        IReadOnlyList<CsoDeepVerifyIssue>? outputIssues = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputVerificationStatus);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputVerificationStatus);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionTaken);
        ArgumentException.ThrowIfNullOrWhiteSpace(conclusion);

        return this with
        {
            RepairMode = repairMode,
            CorruptionDetected = corruptionDetected,
            InputVerificationStatus = inputVerificationStatus,
            OutputVerificationStatus = outputVerificationStatus,
            ActionTaken = actionTaken,
            Conclusion = conclusion,
            InputIssues = inputIssues ?? EmptyIssues,
            OutputIssues = outputIssues ?? EmptyIssues,
        };
    }

    private static void ValidatePaddingBytes(long paddingBytes)
    {
        if (paddingBytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(paddingBytes), "Padding byte count cannot be negative.");
        }
    }

    private static void ValidateBlockCounts(
        int compressedBlocks,
        int storedBlocks,
        int zeroBlocks)
    {
        if (compressedBlocks < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(compressedBlocks), "Compressed block count cannot be negative.");
        }

        if (storedBlocks < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(storedBlocks), "Stored block count cannot be negative.");
        }

        if (zeroBlocks < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(zeroBlocks), "Zero block count cannot be negative.");
        }
    }
}
