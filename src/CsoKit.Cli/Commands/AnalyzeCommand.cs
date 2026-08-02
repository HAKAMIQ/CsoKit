using CsoKit.Core.Formats.Iso;

namespace CsoKit.Cli.Commands;

public static class AnalyzeCommand
{
    public static int Run(string[] args)
    {
        if (!TryParseArgs(args, out AnalyzeCommandOptions options))
        {
            PrintUsage();
            return CliExitCodes.InvalidArguments;
        }

        PspIsoValidationResult result = PspIsoValidator.Validate(
            options.InputPath,
            options.AllowPadding);

        if (options.Json)
        {
            object[] issues = CreateIssuePayloads(result.Issues);

            string? firstCode = null;
            string? firstMessage = null;

            if (result.Issues.Count > 0)
            {
                PspIsoValidationIssue firstIssue = result.Issues[0];
                firstCode = firstIssue.Code;
                firstMessage = firstIssue.Message;
            }

            JsonConsole.Write(new
            {
                schemaVersion = 1,
                command = "analyze",
                success = result.Success,
                input = SafeFullPath(options.InputPath),
                output = (string?)null,
                format = "RawIso",
                warnings = result.Warnings,
                diagnostics = new
                {
                    inputBytes = result.InputBytes,
                    paddingBytes = result.PaddingBytes,
                    hasIso9660PrimaryVolumeDescriptor = result.HasIso9660PrimaryVolumeDescriptor
                },
                psp = new
                {
                    hasPspGame = result.HasPspGame,
                    hasUmdData = result.HasUmdDataBin,
                    hasParamSfo = result.HasParamSfo,
                    hasEbootBin = result.HasEbootBin,
                    discIdFromUmdData = result.DiscIdFromUmdData,
                    discIdFromParamSfo = result.DiscIdFromParamSfo,
                    title = result.Title,
                    category = result.Category,
                    pspSystemVersion = result.PspSystemVersion,
                    warnings = result.Warnings
                },
                issues,
                error = result.Success
                    ? null
                    : new CsoCommandError(
                        firstCode ?? "VerificationFailed",
                        firstMessage ?? "PSP ISO analysis failed.")
            });

            return result.Success ? CliExitCodes.Success : ToExitCode(firstCode);
        }

        Console.WriteLine("PSP ISO Analysis");
        Console.WriteLine("----------------");
        Console.WriteLine($"Input: {SafeFullPath(options.InputPath)}");

        if (result.Success)
        {
            Console.WriteLine("Status: OK");
            Console.WriteLine($"Input bytes: {result.InputBytes:N0}");

            if (!string.IsNullOrWhiteSpace(result.DiscIdFromUmdData))
            {
                Console.WriteLine($"DISC_ID: {result.DiscIdFromUmdData}");
            }

            if (!string.IsNullOrWhiteSpace(result.Title))
            {
                Console.WriteLine($"Title: {result.Title}");
            }

            foreach (string warning in result.Warnings)
            {
                Console.WriteLine($"Warning: {warning}");
            }

            return CliExitCodes.Success;
        }

        Console.Error.WriteLine("Status: FAILED");

        foreach (PspIsoValidationIssue issue in result.Issues)
        {
            Console.Error.WriteLine($"{issue.Code}: {issue.Message}");
        }

        return ToExitCode(result.Issues.Count > 0 ? result.Issues[0].Code : null);
    }

    private static object[] CreateIssuePayloads(
        IReadOnlyList<PspIsoValidationIssue> issues)
    {
        object[] payloads = new object[issues.Count];

        for (int index = 0; index < issues.Count; index++)
        {
            PspIsoValidationIssue issue = issues[index];

            payloads[index] = new
            {
                code = issue.Code,
                message = issue.Message,
                path = issue.Path
            };
        }

        return payloads;
    }

    private static bool TryParseArgs(string[] args, out AnalyzeCommandOptions options)
    {
        options = default!;

        if (args.Length < 1)
        {
            return false;
        }

        string inputPath = args[0];
        bool json = false;
        bool allowPadding = false;

        for (int index = 1; index < args.Length; index++)
        {
            string arg = args[index];

            if (string.Equals(arg, "--json", StringComparison.OrdinalIgnoreCase))
            {
                json = true;
                continue;
            }

            if (string.Equals(arg, "--psp", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(arg, "--allow-padding", StringComparison.OrdinalIgnoreCase))
            {
                allowPadding = true;
                continue;
            }

            return false;
        }

        if (string.IsNullOrWhiteSpace(inputPath))
        {
            return false;
        }

        options = new AnalyzeCommandOptions(inputPath, json, allowPadding);
        return true;
    }

    private static int ToExitCode(string? errorCode)
    {
        return errorCode switch
        {
            "InputNotFound" => CliExitCodes.InputNotFound,
            "IsoNotSectorAligned" or "InvalidIsoSize" => CliExitCodes.InvalidCsoHeader,
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
        Console.Error.WriteLine("Usage: csokit analyze <input.iso> [--psp] [--allow-padding] [--json]");
    }

    private sealed record AnalyzeCommandOptions(
        string InputPath,
        bool Json,
        bool AllowPadding);
}