using CsoKit.Application;
using CsoKit.Core.Formats.Cso;

namespace CsoKit.Cli.Commands;

public static class RepairCommand
{
    public static int Run(string[] args)
    {
        if (!TryParseArgs(args, out RepairCommandOptions options, out string? errorMessage))
        {
            if (HasJsonFlag(args))
            {
                JsonConsole.Write(new
                {
                    schemaVersion = 1,
                    command = "repair",
                    success = false,
                    input = args.Length > 0 ? SafeFullPath(args[0]) : null,
                    output = (string?)null,
                    format = (string?)null,
                    warnings = Array.Empty<string>(),
                    diagnostics = new { },
                    error = new CsoCommandError("InvalidArguments", errorMessage ?? "Invalid repair command arguments.")
                });
            }
            else
            {
                PrintUsage(errorMessage);
            }

            return CliExitCodes.InvalidArguments;
        }

        using CancellationTokenSource cancellation = new();

        void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs eventArgs)
        {
            eventArgs.Cancel = true;

            if (!cancellation.IsCancellationRequested)
            {
                cancellation.Cancel();

                if (!options.Json)
                {
                    Console.Error.WriteLine();
                    Console.Error.WriteLine("Cancellation requested. Cleaning up...");
                }
            }
        }

        Console.CancelKeyPress += OnCancelKeyPress;

        CsoOperationResult operation;

        try
        {
            operation = CsoOperationService.Repair(
                options.InputPath,
                options.OutputPath,
                options.Profile,
                options.Force,
                options.PadLastSector,
                options.DeepVerify,
                options.CodecReport,
                options.CodecReportBlockLimit,
                progress: null,
                cancellationToken: cancellation.Token);
        }
        finally
        {
            Console.CancelKeyPress -= OnCancelKeyPress;
        }

        CsoRepairOperationData? result = operation.Data as CsoRepairOperationData;

        if (options.Json)
        {
            if (operation.Success && result is null)
            {
                throw new InvalidOperationException("Successful repair operation did not return typed repair data.");
            }

            object? metrics = result is null
                ? null
                : new
                {
                    bytesRead = result.BytesRead,
                    bytesWritten = result.BytesWritten,
                    compressedBlocks = result.CompressedBlocks,
                    storedBlocks = result.StoredBlocks,
                    zeroBlocks = result.ZeroBlocks
                };
            object? codecReport = options.CodecReport && result is not null
                ? result.CodecReport
                : null;

            JsonConsole.Write(new
            {
                schemaVersion = 1,
                command = "repair",
                success = operation.Success,
                input = SafeFullPath(options.InputPath),
                output = SafeFullPath(options.OutputPath),
                format = result?.InputFormat ?? operation.Format,
                warnings = Array.Empty<string>(),
                diagnostics = new
                {
                    mode = result?.Mode,
                    usedTempIso = result?.UsedTempIso,
                    fallbackReason = result?.FallbackReason
                },
                inputFormat = result?.InputFormat,
                profile = CsoCompressionProfilePolicy.GetCliName(options.Profile),
                repair = new
                {
                    padLastSector = options.PadLastSector,
                    paddingBytes = result?.PaddingBytes,
                    deepVerify = options.DeepVerify,
                    codecReport = options.CodecReport,
                    codecReportBlockLimit = options.CodecReportBlockLimit,
                    mode = result?.Mode,
                    usedTempIso = result?.UsedTempIso,
                    fallbackReason = result?.FallbackReason
                },
                metrics,
                codecReport,
                error = operation.Success
                    ? null
                    : new CsoCommandError(operation.ErrorCode ?? "RepairNotPossible", GetOperationErrorMessage(operation))
            });

            return operation.Success ? CliExitCodes.Success : ToExitCode(operation.ErrorCode);
        }

        if (!operation.Success)
        {
            Console.Error.WriteLine("Status: FAILED");
            Console.Error.WriteLine($"{operation.ErrorCode}: {GetOperationErrorMessage(operation)}");
            return ToExitCode(operation.ErrorCode);
        }

        if (result is null)
        {
            throw new InvalidOperationException("Successful repair operation did not return typed repair data.");
        }

        Console.WriteLine("CSO Repair");
        Console.WriteLine("----------");
        Console.WriteLine($"Input:  {SafeFullPath(options.InputPath)}");
        Console.WriteLine($"Output: {SafeFullPath(options.OutputPath)}");
        Console.WriteLine("Status: OK");
        Console.WriteLine($"Input format: {result.InputFormat}");
        Console.WriteLine($"Repair mode: {result.Mode}");
        Console.WriteLine($"Used temp ISO: {result.UsedTempIso.ToString().ToLowerInvariant()}");
        Console.WriteLine($"Profile: {CsoCompressionProfilePolicy.GetCliName(options.Profile)}");
        Console.WriteLine($"Bytes read: {result.BytesRead:N0}");
        Console.WriteLine($"Bytes written: {result.BytesWritten:N0}");

        if (result.PaddingBytes > 0)
        {
            Console.WriteLine($"Padding added: {result.PaddingBytes:N0}");
        }

        if (options.CodecReport && result.CodecReport is not null)
        {
            Console.WriteLine("Codec wins:");

            List<KeyValuePair<string, int>> selectedCodecWins =
                [.. result.CodecReport.SelectedCodecWins];

            selectedCodecWins.Sort(static (left, right) =>
                StringComparer.OrdinalIgnoreCase.Compare(left.Key, right.Key));

            foreach (KeyValuePair<string, int> item in selectedCodecWins)
            {
                Console.WriteLine($"  {item.Key}: {item.Value:N0}");
            }
        }

        return CliExitCodes.Success;
    }

    private static bool TryParseArgs(
        string[] args,
        out RepairCommandOptions options,
        out string? errorMessage)
    {
        options = default!;
        errorMessage = null;

        if (args.Length < 1)
        {
            errorMessage = "Missing input path.";
            return false;
        }

        string inputPath = args[0];
        string? outputPath = null;
        bool force = false;
        bool json = false;
        bool padLastSector = false;
        bool codecReport = false;
        int codecReportBlockLimit = 64;
        CsoCompressionProfile profile = CsoCompressionProfile.GameSafe;

        for (int index = 1; index < args.Length; index++)
        {
            string arg = args[index];

            if (string.Equals(arg, "-o", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "--output", StringComparison.OrdinalIgnoreCase))
            {
                if (outputPath is not null)
                {
                    errorMessage = "Output path was provided more than once.";
                    return false;
                }

                if (index + 1 >= args.Length)
                {
                    errorMessage = "Missing output path after -o.";
                    return false;
                }

                outputPath = args[++index];
                continue;
            }

            if (string.Equals(arg, "--profile", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= args.Length)
                {
                    errorMessage = $"Missing profile value after --profile. Supported profiles: {CsoCompressionProfilePolicy.SupportedNamesText}.";
                    return false;
                }

                string profileValue = args[++index];

                if (!CsoCompressionProfilePolicy.TryParse(profileValue, out profile))
                {
                    errorMessage = $"Invalid repair profile '{profileValue}'. Supported profiles: {CsoCompressionProfilePolicy.SupportedNamesText}.";
                    return false;
                }

                if (profile != CsoCompressionProfile.GameSafe)
                {
                    errorMessage = "repair currently supports --profile game-safe only.";
                    return false;
                }

                continue;
            }

            if (string.Equals(arg, "--deep-verify", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "--deep", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(arg, "--repair", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= args.Length)
                {
                    errorMessage = "Missing repair action after --repair.";
                    return false;
                }

                string action = args[++index];

                if (!string.Equals(action, "pad-last-sector", StringComparison.OrdinalIgnoreCase))
                {
                    errorMessage = $"Unsupported repair action: {action}";
                    return false;
                }

                padLastSector = true;
                continue;
            }

            if (string.Equals(arg, "--codec-report", StringComparison.OrdinalIgnoreCase))
            {
                codecReport = true;
                continue;
            }

            if (TryConsumeOptionValue(args, ref index, "--codec-report-block-limit", out string? codecReportBlockLimitValue, out errorMessage))
            {
                if (errorMessage is not null)
                {
                    return false;
                }

                if (!int.TryParse(codecReportBlockLimitValue, out int parsedCodecReportBlockLimit) ||
                    parsedCodecReportBlockLimit < 0)
                {
                    errorMessage = "--codec-report-block-limit must be zero or a positive integer.";
                    return false;
                }

                codecReportBlockLimit = parsedCodecReportBlockLimit;
                continue;
            }

            if (string.Equals(arg, "--force", StringComparison.OrdinalIgnoreCase))
            {
                force = true;
                continue;
            }

            if (string.Equals(arg, "--json", StringComparison.OrdinalIgnoreCase))
            {
                json = true;
                continue;
            }

            errorMessage = $"Unknown repair option: {arg}";
            return false;
        }

        if (string.IsNullOrWhiteSpace(inputPath))
        {
            errorMessage = "Missing input path.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            errorMessage = "Missing output path. Use -o <output.cso>.";
            return false;
        }

        options = new RepairCommandOptions(
            inputPath,
            outputPath,
            force,
            json,
            profile,
            padLastSector,
            DeepVerify: true,
            CodecReport: codecReport,
            CodecReportBlockLimit: codecReportBlockLimit);

        return true;
    }

    private static bool HasJsonFlag(string[] args)
    {
        for (int index = 0; index < args.Length; index++)
        {
            if (string.Equals(args[index], "--json", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string GetOperationErrorMessage(CsoOperationResult operation)
    {
        CsoOperationDetail? detail = operation.DetailLines.LastOrDefault(static item =>
            item.Kind is CsoOperationDetailKind.Text &&
            !string.IsNullOrWhiteSpace(item.Value));

        return detail?.Value ?? operation.Status;
    }

    private static int ToExitCode(string? errorCode)
    {
        return errorCode switch
        {
            "InputNotFound" => CliExitCodes.InputNotFound,
            "OutputAlreadyExists" => CliExitCodes.OutputAlreadyExists,
            "IsoNotSectorAligned" => CliExitCodes.InvalidCsoHeader,
            "ReDumpRequired" or "CorruptCompressedBlock" => CliExitCodes.DecompressionFailed,
            "UnsupportedContainer" => CliExitCodes.UnsupportedCsoVersion,
            "DiskSpacePreflightFailed" => CliExitCodes.NotEnoughDiskSpace,
            "InvalidCodecReportBlockLimit" => CliExitCodes.InvalidArguments,
            "UnsupportedRepairProfile" => CliExitCodes.InvalidArguments,
            "DeepVerifyRequired" => CliExitCodes.InvalidArguments,
            "OperationCanceled" => CliExitCodes.OperationCanceled,
            "OutputFileNameTooShort" or "OutputFileNameTooLong" or "InvalidOutputPath" => CliExitCodes.CannotWriteOutput,
            _ => CliExitCodes.GeneralFailure,
        };
    }

    private static bool TryConsumeOptionValue(
        string[] args,
        ref int index,
        string optionName,
        out string? value,
        out string? errorMessage)
    {
        value = null;
        errorMessage = null;

        string arg = args[index];

        if (string.Equals(arg, optionName, StringComparison.OrdinalIgnoreCase))
        {
            if (index + 1 >= args.Length)
            {
                errorMessage = $"Missing value after {optionName}.";
                return true;
            }

            value = args[index + 1];
            index++;
            return true;
        }

        string prefix = optionName + "=";

        if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            value = arg[prefix.Length..];

            if (string.IsNullOrWhiteSpace(value))
            {
                errorMessage = $"Missing value after {optionName}.";
            }

            return true;
        }

        return false;
    }

    private static string SafeFullPath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            return path;
        }
    }

    private static void PrintUsage(string? errorMessage = null)
    {
        if (!string.IsNullOrWhiteSpace(errorMessage))
        {
            Console.Error.WriteLine(errorMessage);
        }

        Console.Error.WriteLine("Usage: csokit repair <input.iso|input.cso> -o <output.cso> [--profile game-safe] [--repair pad-last-sector] [--deep-verify] [--codec-report] [--codec-report-block-limit <n>] [--force] [--json]");
    }

    private sealed record RepairCommandOptions(
        string InputPath,
        string OutputPath,
        bool Force,
        bool Json,
        CsoCompressionProfile Profile,
        bool PadLastSector,
        bool DeepVerify,
        bool CodecReport,
        int CodecReportBlockLimit);
}