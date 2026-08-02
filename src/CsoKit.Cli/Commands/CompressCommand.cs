using CsoKit.Application;
using CsoKit.Core.Compression;
using CsoKit.Core.Formats.Cso;

namespace CsoKit.Cli.Commands;

public static class CompressCommand
{
    public static int Run(string[] args)
    {
        if (!TryParseArgs(args, out CompressCommandOptions options, out string? parseError))
        {
            string errorMessage = parseError ?? "Invalid compress command arguments.";

            if (HasJsonFlag(args))
            {
                JsonConsole.Write(CsoCompressJsonContract.ArgumentError(errorMessage));
            }
            else
            {
                PrintUsage(errorMessage);
            }

            return CliExitCodes.InvalidArguments;
        }

        using CancellationTokenSource cancellation = new();

        Console.CancelKeyPress += OnCancelKeyPress;

        try
        {
            return options.Measure
                ? RunMeasure(options, cancellation.Token)
                : RunCompress(options, cancellation.Token);
        }
        finally
        {
            Console.CancelKeyPress -= OnCancelKeyPress;
        }

        void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs eventArgs)
        {
            eventArgs.Cancel = true;

            if (!cancellation.IsCancellationRequested)
            {
                cancellation.Cancel();

                if (!options.Quiet && !options.Json)
                {
                    Console.Error.WriteLine();
                    Console.Error.WriteLine("Cancellation requested. Cleaning up...");
                }
            }
        }
    }

    private static int RunMeasure(
        CompressCommandOptions options,
        CancellationToken cancellationToken)
    {
        CsoCompressionProfileSettings profileSettings = CsoCompressionProfilePolicy.Create(options.Profile);

        if (!options.Quiet && !options.Json)
        {
            Console.WriteLine("CSO Measure");
            Console.WriteLine("-----------");
            Console.WriteLine($"Input: {SafeFullPath(options.InputPath)}");
            Console.WriteLine("Mode:  measure only; no output file will be written.");
        }

        ConsoleCompressProgress? progress = options.Quiet || options.Json
            ? null
            : new ConsoleCompressProgress();

        CsoOperationResult operation = CsoOperationService.Measure(
            options.InputPath,
            options.Profile,
            options.BlockSize,
            options.UseZopfli,
            progress,
            cancellationToken);

        progress?.FinishLine();

        if (operation.Data is not CsoMeasureOperationData result)
        {
            throw new InvalidOperationException("Measure operation did not return typed measure data.");
        }

        if (options.Json)
        {
            JsonConsole.Write(CsoCompressJsonContract.Measure(
                SafeFullPath(options.InputPath),
                profileSettings,
                operation,
                options.BlockSize,
                options.WorkerCount,
                options.UseZopfli,
                deepVerify: false,
                codecReport: options.CodecReport,
                codecReportBlockLimit: options.CodecReportBlockLimit));

            return operation.Success
                ? CliExitCodes.Success
                : ToExitCode(operation.ErrorCode);
        }

        if (operation.Success)
        {
            if (!options.Quiet)
            {
                Console.WriteLine("Status: OK");
                Console.WriteLine($"Original size: {result.OriginalBytes:N0}");
                Console.WriteLine($"Estimated CSO size: {result.EstimatedBytes:N0}");
                Console.WriteLine($"Estimated ratio: {result.EstimatedRatio:P2}");
                Console.WriteLine($"Estimated saved space: {result.EstimatedSavedBytes:N0}");

                if (result.EstimatedGrowthBytes > 0)
                {
                    Console.WriteLine($"Estimated growth: {result.EstimatedGrowthBytes:N0}");
                }

                Console.WriteLine($"Total blocks: {result.TotalBlocks:N0}");
                Console.WriteLine($"Compressed blocks: {result.CompressedBlocks:N0}");
                Console.WriteLine($"Stored blocks: {result.StoredBlocks:N0}");
                PrintProfileSummary(profileSettings);
                PrintCompressionOptions(options);
            }

            return CliExitCodes.Success;
        }

        Console.Error.WriteLine("Status: FAILED");
        Console.Error.WriteLine($"{operation.ErrorCode}: {GetOperationErrorMessage(operation)}");
        PrintProfileSummary(profileSettings, Console.Error);
        PrintCompressionOptions(options, Console.Error);

        return ToExitCode(operation.ErrorCode);
    }

    private static int RunCompress(
        CompressCommandOptions options,
        CancellationToken cancellationToken)
    {
        CsoCompressionProfileSettings profileSettings = CsoCompressionProfilePolicy.Create(options.Profile);
        string outputPath = options.OutputPath ?? CsoOperationService.CreateSuggestedCompressOutputPath(options.InputPath);
        bool autoOutput = options.OutputPath is null;

        if (!options.Quiet && !options.Json)
        {
            Console.WriteLine("CSO Compression");
            Console.WriteLine("---------------");
            Console.WriteLine($"Input:  {SafeFullPath(options.InputPath)}");
            Console.WriteLine($"Output: {SafeFullPath(outputPath)}");

            if (autoOutput)
            {
                Console.WriteLine("Output mode: same folder; auto-named without overwriting existing files.");
            }
        }

        ConsoleCompressProgress? progress = options.Quiet || options.Json
            ? null
            : new ConsoleCompressProgress();

        CsoOperationResult operation = CsoOperationService.Compress(
            options.InputPath,
            outputPath,
            options.Profile,
            options.BlockSize,
            options.WorkerCount,
            options.Force && !autoOutput,
            options.DeepVerify,
            options.CodecReport,
            options.UseZopfli,
            options.CodecReportBlockLimit,
            progress,
            cancellationToken);

        progress?.FinishLine();

        if (options.Json)
        {
            JsonConsole.Write(CsoCompressJsonContract.Write(
                SafeFullPath(options.InputPath),
                SafeFullPath(outputPath),
                options.Force && !autoOutput,
                autoOutput,
                profileSettings,
                operation,
                options.BlockSize,
                options.WorkerCount,
                options.UseZopfli,
                options.DeepVerify,
                options.CodecReport,
                options.CodecReportBlockLimit));

            return operation.Success
                ? CliExitCodes.Success
                : ToExitCode(operation.ErrorCode);
        }

        if (!operation.Success)
        {
            Console.Error.WriteLine("Status: FAILED");
            Console.Error.WriteLine($"{operation.ErrorCode}: {GetOperationErrorMessage(operation)}");
            PrintProfileSummary(profileSettings, Console.Error);
            PrintCompressionOptions(options, Console.Error);

            return ToExitCode(operation.ErrorCode);
        }

        if (operation.Data is not CsoCompressOperationData result)
        {
            throw new InvalidOperationException("Successful compress operation did not return typed compression data.");
        }

        if (!options.Quiet)
        {
            Console.WriteLine("Status: OK");
            Console.WriteLine($"Bytes read: {result.BytesRead:N0}");
            Console.WriteLine($"Bytes written: {result.BytesWritten:N0}");
            Console.WriteLine($"Compressed blocks: {result.CompressedBlocks:N0}");
            Console.WriteLine($"Stored blocks: {result.StoredBlocks:N0}");
            PrintProfileSummary(profileSettings);
            PrintCompressionOptions(options);

            if (options.CodecReport)
            {
                PrintCodecReport(result.CodecWins);
            }
        }

        return CliExitCodes.Success;
    }

    private static bool TryParseArgs(
        string[] args,
        out CompressCommandOptions options,
        out string? errorMessage)
    {
        options = default!;
        errorMessage = null;

        if (args.Length < 1)
        {
            errorMessage = "Missing input ISO path.";
            return false;
        }

        string inputPath = args[0];
        string? outputPath = null;
        bool force = false;
        bool quiet = false;
        bool json = false;
        bool measure = false;
        bool fastAlias = false;
        bool profileExplicit = false;
        uint blockSize = CsoCompressor.DefaultBlockSize;
        int workerCount = CsoWorkerPolicy.GetDefaultWorkerCount();
        bool useZopfli = false;
        bool deepVerify = false;
        bool codecReport = false;
        int codecReportBlockLimit = 64;
        CsoCompressionProfile profile = CsoCompressionProfilePolicy.DefaultProfile;

        for (int index = 1; index < args.Length; index++)
        {
            string arg = args[index];

            if (string.Equals(arg, "-o", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "--output", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "--output-path", StringComparison.OrdinalIgnoreCase))
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

                outputPath = args[index + 1];
                index++;
                continue;
            }

            if (TryConsumeOptionValue(args, ref index, "--threads", out string? threadsValue, out errorMessage))
            {
                if (errorMessage is not null)
                {
                    return false;
                }

                if (!int.TryParse(threadsValue, out int parsedWorkerCount))
                {
                    errorMessage = "--threads must be a positive integer within the safe worker limit.";
                    return false;
                }

                if (!CsoWorkerPolicy.TryValidate(parsedWorkerCount, out string? workerError))
                {
                    errorMessage = workerError ?? "--threads must be a positive integer within the safe worker limit.";
                    return false;
                }

                workerCount = parsedWorkerCount;
                continue;
            }

            if (TryConsumeOptionValue(args, ref index, "--block", out string? blockValue, out errorMessage))
            {
                if (errorMessage is not null)
                {
                    return false;
                }

                if (!TryParseBlockSize(blockValue, out uint parsedBlockSize))
                {
                    errorMessage = "--block must be a positive byte size, optionally using K or M suffixes.";
                    return false;
                }

                blockSize = parsedBlockSize;
                continue;
            }

            if (string.Equals(arg, "--zopfli", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "--use-zopfli", StringComparison.OrdinalIgnoreCase))
            {
                useZopfli = true;
                continue;
            }

            if (string.Equals(arg, "--deep-verify", StringComparison.OrdinalIgnoreCase))
            {
                deepVerify = true;
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

            if (string.Equals(arg, "--profile", StringComparison.OrdinalIgnoreCase))
            {
                if (profileExplicit)
                {
                    errorMessage = "Compression profile was provided more than once.";
                    return false;
                }

                if (index + 1 >= args.Length)
                {
                    errorMessage = $"Missing profile value after --profile. Supported profiles: {CsoCompressionProfilePolicy.SupportedNamesText}.";
                    return false;
                }

                string profileValue = args[index + 1];

                if (!CsoCompressionProfilePolicy.TryParse(profileValue, out CsoCompressionProfile parsedProfile))
                {
                    errorMessage = $"Invalid compression profile '{profileValue}'. Supported profiles: {CsoCompressionProfilePolicy.SupportedNamesText}.";
                    return false;
                }

                if (fastAlias && parsedProfile != CsoCompressionProfile.Fast)
                {
                    errorMessage = BuildFastProfileConflictMessage(parsedProfile);
                    return false;
                }

                profile = parsedProfile;
                profileExplicit = true;
                index++;
                continue;
            }

            if (string.Equals(arg, "--fast", StringComparison.OrdinalIgnoreCase))
            {
                if (profileExplicit && profile != CsoCompressionProfile.Fast)
                {
                    errorMessage = BuildFastProfileConflictMessage(profile);
                    return false;
                }

                profile = CsoCompressionProfile.Fast;
                fastAlias = true;
                continue;
            }

            if (string.Equals(arg, "--measure", StringComparison.OrdinalIgnoreCase))
            {
                measure = true;
                continue;
            }

            if (string.Equals(arg, "--force", StringComparison.OrdinalIgnoreCase))
            {
                force = true;
                continue;
            }

            if (string.Equals(arg, "--quiet", StringComparison.OrdinalIgnoreCase))
            {
                quiet = true;
                continue;
            }

            if (string.Equals(arg, "--json", StringComparison.OrdinalIgnoreCase))
            {
                json = true;
                continue;
            }

            errorMessage = $"Unknown compress option: {arg}";
            return false;
        }

        if (string.IsNullOrWhiteSpace(inputPath))
        {
            errorMessage = "Missing input ISO path.";
            return false;
        }

        if (measure && outputPath is not null)
        {
            errorMessage = "--measure does not write output and cannot be combined with -o.";
            return false;
        }

        options = new CompressCommandOptions(
            inputPath,
            outputPath,
            force,
            quiet,
            json,
            measure,
            profile,
            blockSize,
            workerCount,
            useZopfli,
            deepVerify || profile == CsoCompressionProfile.GameSafe,
            codecReport,
            codecReportBlockLimit);

        return true;
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

    private static bool TryParseBlockSize(string? value, out uint blockSize)
    {
        blockSize = 0;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string text = value.Trim();
        ulong multiplier = 1;

        char suffix = text[^1];
        if (suffix is 'k' or 'K')
        {
            multiplier = 1024;
            text = text[..^1];
        }
        else if (suffix is 'm' or 'M')
        {
            multiplier = 1024UL * 1024UL;
            text = text[..^1];
        }

        if (!ulong.TryParse(text, out ulong parsed) ||
            parsed == 0 ||
            parsed > uint.MaxValue / multiplier)
        {
            return false;
        }

        blockSize = checked((uint)(parsed * multiplier));
        return true;
    }

    private static string BuildFastProfileConflictMessage(CsoCompressionProfile profile)
    {
        string profileName = CsoCompressionProfilePolicy.GetCliName(profile);
        return $"--fast cannot be combined with --profile {profileName}. Use --profile fast or remove --fast.";
    }

    private static int ToExitCode(string? errorCode)
    {
        return errorCode switch
        {
            "InputNotFound" => CliExitCodes.InputNotFound,
            "OutputAlreadyExists" => CliExitCodes.OutputAlreadyExists,
            "NotEnoughDiskSpace" => CliExitCodes.NotEnoughDiskSpace,
            "OperationCanceled" => CliExitCodes.OperationCanceled,
            "SameInputOutputPath" or "OutputPathIsDirectory" or "OutputDirectoryNotFound" or "InvalidOutputPath" or
            "OutputFileNameTooShort" or "OutputFileNameTooLong" => CliExitCodes.CannotWriteOutput,
            "OutputAccessDenied" or "CompressionIoFailed" or "OutputDriveCheckFailed" or "OutputDriveNotReady" or "OutputDriveNotFound" => CliExitCodes.CannotWriteOutput,
            "InputAccessDenied" or "MeasureIoFailed" => CliExitCodes.CannotWriteOutput,
            "InvalidBlockSize" or "BlockSizeTooLarge" or "InvalidThreadCount" or "InvalidCodecReportBlockLimit" or "InvalidInputSize" => CliExitCodes.InvalidCsoHeader,
            _ => CliExitCodes.CompressionFailed
        };
    }

    private static string GetOperationErrorMessage(CsoOperationResult operation)
    {
        CsoOperationDetail? detail = operation.DetailLines.LastOrDefault(static item =>
            item.Kind is CsoOperationDetailKind.Text &&
            !string.IsNullOrWhiteSpace(item.Value));

        return detail?.Value ?? operation.Status;
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

    private static void PrintProfileSummary(CsoCompressionProfileSettings profileSettings)
    {
        PrintProfileSummary(profileSettings, Console.Out);
    }

    private static void PrintProfileSummary(
        CsoCompressionProfileSettings profileSettings,
        TextWriter writer)
    {
        writer.WriteLine($"Profile: {profileSettings.CliName}");
        writer.WriteLine($"Fast: {profileSettings.IsFast.ToString().ToLowerInvariant()}");
        writer.WriteLine($"Level: {profileSettings.Level}");
    }

    private static void PrintCompressionOptions(CompressCommandOptions options)
    {
        PrintCompressionOptions(options, Console.Out);
    }

    private static void PrintCompressionOptions(
        CompressCommandOptions options,
        TextWriter writer)
    {
        writer.WriteLine($"Block size: {options.BlockSize:N0}");
        writer.WriteLine($"Threads: {options.WorkerCount:N0}");
        writer.WriteLine($"Zopfli: {options.UseZopfli.ToString().ToLowerInvariant()}");
        writer.WriteLine($"Deep verify: {options.DeepVerify.ToString().ToLowerInvariant()}");

        if (options.CodecReport)
        {
            writer.WriteLine($"Codec report block limit: {options.CodecReportBlockLimit:N0}");
        }
    }

    private static void PrintCodecReport(IReadOnlyDictionary<string, int> codecWins)
    {
        Console.WriteLine("Codec wins:");

        List<KeyValuePair<string, int>> orderedCodecWins = [.. codecWins];
        orderedCodecWins.Sort(static (left, right) =>
            StringComparer.OrdinalIgnoreCase.Compare(left.Key, right.Key));

        foreach (KeyValuePair<string, int> item in orderedCodecWins)
        {
            Console.WriteLine($"  {item.Key}: {item.Value:N0}");
        }
    }

    private static void PrintUsage(string? errorMessage = null)
    {
        if (!string.IsNullOrWhiteSpace(errorMessage))
        {
            Console.Error.WriteLine(errorMessage);
        }

        Console.Error.WriteLine($"Usage: csokit compress <input.iso> [-o <output.cso>] [--profile <{CsoCompressionProfilePolicy.SupportedNamesText}>] [--fast] [--threads <n>] [--block <bytes>] [--zopfli] [--deep-verify] [--codec-report] [--codec-report-block-limit <n>] [--force] [--quiet] [--json]");
        Console.Error.WriteLine($"       csokit compress <input.iso> --measure [--profile <{CsoCompressionProfilePolicy.SupportedNamesText}>] [--fast] [--block <bytes>] [--zopfli] [--quiet] [--json]");
    }

    private sealed record CompressCommandOptions(
        string InputPath,
        string? OutputPath,
        bool Force,
        bool Quiet,
        bool Json,
        bool Measure,
        CsoCompressionProfile Profile,
        uint BlockSize,
        int WorkerCount,
        bool UseZopfli,
        bool DeepVerify,
        bool CodecReport,
        int CodecReportBlockLimit);

    private sealed class ConsoleCompressProgress : IProgress<double>
    {
        private bool hasWritten;

        public void Report(double value)
        {
            hasWritten = true;
            Console.Write($"\rProgress: {value,6:0.0}%");
        }

        public void FinishLine()
        {
            if (hasWritten)
            {
                Console.WriteLine();
            }
        }
    }
}