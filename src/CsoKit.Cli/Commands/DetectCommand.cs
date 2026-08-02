using CsoKit.Core.Formats.DiscImage;

namespace CsoKit.Cli.Commands;

public static class DetectCommand
{
    public static int Run(string[] args)
    {
        if (!TryParseArgs(args, out DetectCommandOptions options))
        {
            PrintUsage();
            return CliExitCodes.InvalidArguments;
        }

        FormatDetectionResult result = FormatDetector.Detect(options.InputPath);

        if (options.Json)
        {
            JsonConsole.Write(new
            {
                schemaVersion = 1,
                command = "detect",
                success = result.Success,
                input = SafeFullPath(options.InputPath),
                output = (string?)null,
                format = result.Success ? result.Format.ToString() : null,
                warnings = result.Warnings,
                diagnostics = new
                {
                    magic = result.Magic,
                    headerSize = result.HeaderSize,
                    uncompressedSize = result.UncompressedSize,
                    blockSize = result.BlockSize,
                    indexShift = result.IndexShift,
                    sectorCount = result.SectorCount
                },
                magic = result.Magic,
                headerSize = result.HeaderSize,
                uncompressedSize = result.UncompressedSize,
                blockSize = result.BlockSize,
                indexShift = result.IndexShift,
                sectorCount = result.SectorCount,
                error = result.Success
                    ? null
                    : new CsoCommandError(result.ErrorCode ?? "DetectFailed", result.ErrorMessage ?? "Format detection failed.")
            });

            return result.Success ? CliExitCodes.Success : ToExitCode(result.ErrorCode);
        }

        if (!result.Success)
        {
            Console.Error.WriteLine($"{result.ErrorCode}: {result.ErrorMessage}");
            return ToExitCode(result.ErrorCode);
        }

        Console.WriteLine("Format Detection");
        Console.WriteLine("----------------");
        Console.WriteLine($"Input:  {SafeFullPath(options.InputPath)}");
        Console.WriteLine($"Format: {result.Format}");
        Console.WriteLine($"Magic:  {result.Magic}");

        if (result.HeaderSize is not null)
        {
            Console.WriteLine($"Header size:       {result.HeaderSize:N0}");
            Console.WriteLine($"Uncompressed size: {result.UncompressedSize:N0}");
            Console.WriteLine($"Block size:        {result.BlockSize:N0}");
            Console.WriteLine($"Index shift:       {result.IndexShift}");
            Console.WriteLine($"Sector count:      {result.SectorCount:N0}");
        }

        foreach (string warning in result.Warnings)
        {
            Console.WriteLine($"Warning: {warning}");
        }

        return CliExitCodes.Success;
    }

    private static bool TryParseArgs(string[] args, out DetectCommandOptions options)
    {
        options = default!;

        if (args.Length is not (1 or 2))
        {
            return false;
        }

        string inputPath = args[0];
        bool json = false;

        for (int index = 1; index < args.Length; index++)
        {
            if (string.Equals(args[index], "--json", StringComparison.OrdinalIgnoreCase))
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

        options = new DetectCommandOptions(inputPath, json);
        return true;
    }

    private static int ToExitCode(string? errorCode)
    {
        return errorCode switch
        {
            "InputNotFound" => CliExitCodes.InputNotFound,
            _ => CliExitCodes.GeneralFailure,
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
        Console.Error.WriteLine("Usage: csokit detect <input> [--json]");
    }

    private sealed record DetectCommandOptions(string InputPath, bool Json);
}