using CsoKit.Application;
using CsoKit.Core.Formats.Cso;

namespace CsoKit.Cli.Commands;

public static class CsoCompressJsonContract
{
    public const int CurrentSchemaVersion = 1;

    public static CsoMeasureJsonOutput Measure(
        string input,
        CsoCompressionProfileSettings profileSettings,
        CsoMeasureResult result,
        uint blockSize = CsoCompressor.DefaultBlockSize,
        int workerCount = 1,
        bool useZopfli = false,
        bool deepVerify = false,
        bool codecReport = false,
        int codecReportBlockLimit = 64)
    {
        ArgumentNullException.ThrowIfNull(profileSettings);
        ArgumentNullException.ThrowIfNull(result);

        return new CsoMeasureJsonOutput(
            CurrentSchemaVersion,
            "compress",
            "measure",
            result.Success,
            input,
            (string?)null,
            "RawIso",
            [],
            new
            {
                mode = "measure",
                profile = profileSettings.CliName,
                blockSize,
                workerCount,
                useZopfli,
                deepVerify,
                codecReport,
                codecReportBlockLimit
            },
            new CsoCompressJsonOptions(
                CsoProfileOutput.From(profileSettings),
                false,
                false,
                blockSize,
                workerCount,
                useZopfli,
                deepVerify,
                codecReport,
                codecReportBlockLimit),
            new CsoMeasureJsonMetrics(
                result.OriginalBytes,
                result.EstimatedBytes,
                result.EstimatedRatio,
                result.EstimatedSavedBytes,
                result.EstimatedGrowthBytes,
                result.TotalBlocks,
                result.CompressedBlocks,
                result.StoredBlocks),
            result.Success ? null : Error(result.ErrorCode, result.ErrorMessage));
    }

    public static CsoWriteJsonOutput Write(
        string input,
        string output,
        bool force,
        bool autoOutput,
        CsoCompressionProfileSettings profileSettings,
        CsoCompressResult result,
        uint blockSize = CsoCompressor.DefaultBlockSize,
        int workerCount = 1,
        bool useZopfli = false,
        bool deepVerify = false,
        bool codecReport = false,
        int codecReportBlockLimit = 64)
    {
        ArgumentNullException.ThrowIfNull(profileSettings);
        ArgumentNullException.ThrowIfNull(result);

        return new CsoWriteJsonOutput(
            CurrentSchemaVersion,
            "compress",
            "write",
            result.Success,
            input,
            output,
            "Cso1",
            [],
            new
            {
                mode = "write",
                profile = profileSettings.CliName,
                blockSize,
                workerCount,
                useZopfli,
                deepVerify,
                codecReport,
                codecReportBlockLimit
            },
            new CsoCompressJsonOptions(
                CsoProfileOutput.From(profileSettings),
                force,
                autoOutput,
                blockSize,
                workerCount,
                useZopfli,
                deepVerify,
                codecReport,
                codecReportBlockLimit),
            new CsoWriteJsonMetrics(
                result.BytesRead,
                result.BytesWritten,
                result.CompressedBlocks,
                result.StoredBlocks,
                result.ZeroBlocks,
                result.EffectiveCodecWins),
            codecReport ? result.CodecTrialSummary : null,
            result.Success ? null : Error(result.ErrorCode, result.ErrorMessage));
    }

    public static CsoMeasureJsonOutput Measure(
        string input,
        CsoCompressionProfileSettings profileSettings,
        CsoOperationResult operation,
        uint blockSize = CsoCompressor.DefaultBlockSize,
        int workerCount = 1,
        bool useZopfli = false,
        bool deepVerify = false,
        bool codecReport = false,
        int codecReportBlockLimit = 64)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (operation.Data is not CsoMeasureOperationData result)
        {
            throw new ArgumentException("Operation result does not contain measure data.", nameof(operation));
        }

        return new CsoMeasureJsonOutput(
            CurrentSchemaVersion,
            "compress",
            "measure",
            operation.Success,
            input,
            (string?)null,
            "RawIso",
            [],
            new
            {
                mode = "measure",
                profile = profileSettings.CliName,
                blockSize,
                workerCount,
                useZopfli,
                deepVerify,
                codecReport,
                codecReportBlockLimit
            },
            new CsoCompressJsonOptions(
                CsoProfileOutput.From(profileSettings),
                false,
                false,
                blockSize,
                workerCount,
                useZopfli,
                deepVerify,
                codecReport,
                codecReportBlockLimit),
            new CsoMeasureJsonMetrics(
                result.OriginalBytes,
                result.EstimatedBytes,
                result.EstimatedRatio,
                result.EstimatedSavedBytes,
                result.EstimatedGrowthBytes,
                result.TotalBlocks,
                result.CompressedBlocks,
                result.StoredBlocks),
            operation.Success ? null : Error(operation.ErrorCode, GetErrorMessage(operation)));
    }

    public static CsoWriteJsonOutput Write(
        string input,
        string output,
        bool force,
        bool autoOutput,
        CsoCompressionProfileSettings profileSettings,
        CsoOperationResult operation,
        uint blockSize = CsoCompressor.DefaultBlockSize,
        int workerCount = 1,
        bool useZopfli = false,
        bool deepVerify = false,
        bool codecReport = false,
        int codecReportBlockLimit = 64)
    {
        ArgumentNullException.ThrowIfNull(operation);

        CsoCompressOperationData? result = operation.Data as CsoCompressOperationData;

        if (operation.Success && result is null)
        {
            throw new ArgumentException("Successful operation result does not contain compression data.", nameof(operation));
        }

        CsoWriteJsonMetrics? metrics = result is null
            ? null
            : new CsoWriteJsonMetrics(
                result.BytesRead,
                result.BytesWritten,
                result.CompressedBlocks,
                result.StoredBlocks,
                result.ZeroBlocks,
                result.CodecWins);

        return new CsoWriteJsonOutput(
            CurrentSchemaVersion,
            "compress",
            "write",
            operation.Success,
            input,
            output,
            operation.Format ?? (operation.Success ? "Cso1" : null),
            [],
            new
            {
                mode = "write",
                profile = profileSettings.CliName,
                blockSize,
                workerCount,
                useZopfli,
                deepVerify,
                codecReport,
                codecReportBlockLimit
            },
            new CsoCompressJsonOptions(
                CsoProfileOutput.From(profileSettings),
                force,
                autoOutput,
                blockSize,
                workerCount,
                useZopfli,
                deepVerify,
                codecReport,
                codecReportBlockLimit),
            metrics,
            codecReport && result is not null ? result.CodecReport : null,
            operation.Success ? null : Error(operation.ErrorCode, GetErrorMessage(operation)));
    }

    private static string GetErrorMessage(CsoOperationResult operation)
    {
        CsoOperationDetail? text = operation.DetailLines.LastOrDefault(static detail =>
            detail.Kind is CsoOperationDetailKind.Text &&
            !string.IsNullOrWhiteSpace(detail.Value));

        return text?.Value ?? operation.Status;
    }

    public static CsoArgumentErrorJsonOutput ArgumentError(string message)
    {
        return new CsoArgumentErrorJsonOutput(
            CurrentSchemaVersion,
            "compress",
            "arguments",
            Success: false,
            Input: (string?)null,
            Output: (string?)null,
            Format: (string?)null,
            Warnings: [],
            Diagnostics: new { },
            Error("InvalidArguments", message));
    }

    private static CsoCommandError Error(string? code, string? message)
    {
        return new CsoCommandError(
            string.IsNullOrWhiteSpace(code) ? "Unknown" : code,
            string.IsNullOrWhiteSpace(message) ? "Command failed." : message);
    }
}

public sealed record CsoCompressJsonOptions(
    CsoProfileOutput Profile,
    bool Force,
    bool AutoOutput,
    uint BlockSize,
    int Threads,
    bool Zopfli,
    bool DeepVerify,
    bool CodecReport,
    int CodecReportBlockLimit = 64);

public sealed record CsoMeasureJsonMetrics(
    ulong OriginalBytes,
    ulong EstimatedBytes,
    double EstimatedRatio,
    ulong EstimatedSavedBytes,
    ulong EstimatedGrowthBytes,
    int TotalBlocks,
    int CompressedBlocks,
    int StoredBlocks);

public sealed record CsoWriteJsonMetrics(
    ulong BytesRead,
    ulong BytesWritten,
    int CompressedBlocks,
    int StoredBlocks,
    int ZeroBlocks,
    IReadOnlyDictionary<string, int> CodecWins);

public sealed record CsoMeasureJsonOutput(
    int SchemaVersion,
    string Command,
    string Mode,
    bool Success,
    string Input,
    string? Output,
    string? Format,
    string[] Warnings,
    object Diagnostics,
    CsoCompressJsonOptions Options,
    CsoMeasureJsonMetrics Metrics,
    CsoCommandError? Error);

public sealed record CsoWriteJsonOutput(
    int SchemaVersion,
    string Command,
    string Mode,
    bool Success,
    string Input,
    string Output,
    string? Format,
    string[] Warnings,
    object Diagnostics,
    CsoCompressJsonOptions Options,
    CsoWriteJsonMetrics? Metrics,
    object? CodecReport,
    CsoCommandError? Error);

public sealed record CsoArgumentErrorJsonOutput(
    int SchemaVersion,
    string Command,
    string Mode,
    bool Success,
    string? Input,
    string? Output,
    string? Format,
    string[] Warnings,
    object Diagnostics,
    CsoCommandError Error);