using CsoKit.Application;

namespace CsoKit.Cli.Commands;

public static class DecompressCommand
{
    public static int Run(string[] args)
    {
        if (!TryParseArgs(args, out DecompressCommandOptions options))
        {
            PrintUsage();
            return CliExitCodes.InvalidArguments;
        }

        using CancellationTokenSource cancellation = new();

        void CancelHandler(object? sender, ConsoleCancelEventArgs eventArgs)
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

        Console.CancelKeyPress += CancelHandler;

        try
        {
            string outputPath = options.OutputPath ?? CsoOperationService.CreateSuggestedDecompressOutputPath(options.InputPath);
            bool autoOutput = options.OutputPath is null;

            if (!options.Quiet && !options.Json)
            {
                Console.WriteLine("CSO Decompression");
                Console.WriteLine("-----------------");
                Console.WriteLine($"Input:  {SafeFullPath(options.InputPath)}");
                Console.WriteLine($"Output: {SafeFullPath(outputPath)}");

                if (autoOutput)
                {
                    Console.WriteLine("Output mode: same folder; auto-named without overwriting existing files.");
                }
            }

            ConsoleDecompressProgress? progress = options.Quiet || options.Json
                ? null
                : new ConsoleDecompressProgress();

            CsoOperationResult operation = CsoOperationService.Decompress(
                options.InputPath,
                outputPath,
                options.Force && !autoOutput,
                progress,
                cancellation.Token);

            progress?.FinishLine();

            CsoDecompressOperationData? result = operation.Data as CsoDecompressOperationData;

            if (options.Json)
            {
                if (operation.Success && result is null)
                {
                    throw new InvalidOperationException("Successful decompress operation did not return typed decompression data.");
                }

                object? metrics = result is null
                    ? null
                    : new { bytesWritten = result.BytesWritten };

                JsonConsole.Write(new
                {
                    schemaVersion = 1,
                    command = "decompress",
                    success = operation.Success,
                    input = SafeFullPath(options.InputPath),
                    output = SafeFullPath(outputPath),
                    format = result?.InputFormat ?? operation.Format,
                    warnings = Array.Empty<string>(),
                    diagnostics = new
                    {
                        force = options.Force && !autoOutput,
                        autoOutput,
                        bytesWritten = result?.BytesWritten
                    },
                    force = options.Force && !autoOutput,
                    autoOutput,
                    bytesWritten = result?.BytesWritten,
                    metrics,
                    error = operation.Success
                        ? null
                        : new CsoCommandError(operation.ErrorCode ?? "DecompressionFailed", GetOperationErrorMessage(operation))
                });

                return operation.Success
                    ? CliExitCodes.Success
                    : ToExitCode(operation.ErrorCode);
            }

            if (!operation.Success)
            {
                Console.Error.WriteLine("Status: FAILED");
                Console.Error.WriteLine($"{operation.ErrorCode}: {GetOperationErrorMessage(operation)}");

                return ToExitCode(operation.ErrorCode);
            }

            if (result is null)
            {
                throw new InvalidOperationException("Successful decompress operation did not return typed decompression data.");
            }

            if (!options.Quiet)
            {
                Console.WriteLine("Status: OK");
                Console.WriteLine($"Bytes written: {result.BytesWritten:N0}");
            }

            return CliExitCodes.Success;
        }
        finally
        {
            Console.CancelKeyPress -= CancelHandler;
        }
    }

    private static bool TryParseArgs(
        string[] args,
        out DecompressCommandOptions options)
    {
        options = default!;

        if (args.Length < 1)
        {
            return false;
        }

        string inputPath = args[0];
        string? outputPath = null;
        bool force = false;
        bool quiet = false;
        bool json = false;

        for (int index = 1; index < args.Length; index++)
        {
            string arg = args[index];

            if (string.Equals(arg, "-o", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "--output", StringComparison.OrdinalIgnoreCase))
            {
                if (outputPath is not null || index + 1 >= args.Length)
                {
                    return false;
                }

                outputPath = args[index + 1];
                index++;
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

            return false;
        }

        if (string.IsNullOrWhiteSpace(inputPath))
        {
            return false;
        }

        options = new DecompressCommandOptions(
            inputPath,
            outputPath,
            force,
            quiet,
            json);

        return true;
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
            "UnsupportedVersion" or "UnsupportedCsoVersion" or "UnsupportedDecompressionVersion" => CliExitCodes.UnsupportedCsoVersion,
            "OutputAlreadyExists" => CliExitCodes.OutputAlreadyExists,
            "NotEnoughDiskSpace" => CliExitCodes.NotEnoughDiskSpace,
            "OperationCanceled" => CliExitCodes.OperationCanceled,
            "SameInputOutputPath" or "OutputPathIsDirectory" or "OutputDirectoryNotFound" or "InvalidOutputPath" or
            "OutputFileNameTooShort" or "OutputFileNameTooLong" => CliExitCodes.CannotWriteOutput,
            "OutputAccessDenied" or "DecompressionIoFailed" or "OutputDriveCheckFailed" or "OutputDriveNotReady" or "OutputDriveNotFound" => CliExitCodes.CannotWriteOutput,
            "InvalidMagic" or "HeaderTooSmall" or "InvalidHeaderSize" or "InvalidUncompressedSize" or "InvalidBlockSize" or "BlockSizeTooLarge" or "InvalidIndexShift"
                => CliExitCodes.InvalidCsoHeader,
            "IndexTableTruncated" or
            "IndexEntryTruncated" or
            "IndexOffsetsNotMonotonic" or
            "IndexOffsetPastEndOfFile" or
            "FinalOffsetPastEndOfFile" or
            "FirstDataOffsetBeforeIndexEnd" or
            "IndexEntryCountMismatch" or
            "InvalidV2FinalSentinel" or
            "CsoV2FinalSentinelHighBit"
                => CliExitCodes.CorruptIndexTable,
            _ => CliExitCodes.DecompressionFailed
        };
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

    private static void PrintUsage()
    {
        Console.Error.WriteLine("Usage: csokit decompress <input.cso> [-o <output.iso>] [--force] [--quiet] [--json]");
    }

    private sealed record DecompressCommandOptions(
        string InputPath,
        string? OutputPath,
        bool Force,
        bool Quiet,
        bool Json);

    private sealed class ConsoleDecompressProgress : IProgress<double>
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