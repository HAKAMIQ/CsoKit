using CsoKit.Application;

namespace CsoKit.Cli.Commands;

public static class VerifyCommand
{
    public static int Run(string[] args)
    {
        if (!TryParseArgs(args, out VerifyCommandOptions options))
        {
            PrintUsage();
            return CliExitCodes.InvalidArguments;
        }

        using CancellationTokenSource cancellation = new();

        void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs eventArgs)
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        }

        Console.CancelKeyPress += OnCancelKeyPress;

        try
        {
            CsoOperationResult operation = CsoOperationService.Verify(
                options.InputPath,
                options.Deep,
                options.Sha256,
                progress: null,
                cancellationToken: cancellation.Token);

            if (operation.Data is not CsoVerifyOperationData result)
            {
                throw new InvalidOperationException("Verify operation did not return typed verification data.");
            }

            return options.Json
                ? WriteJson(options, operation, result)
                : WriteText(options, operation, result);
        }
        catch (OperationCanceledException)
        {
            return CliExitCodes.OperationCanceled;
        }
        finally
        {
            Console.CancelKeyPress -= OnCancelKeyPress;
        }
    }

    private static int WriteJson(
        VerifyCommandOptions options,
        CsoOperationResult operation,
        CsoVerifyOperationData result)
    {
        object? header = result.Header is null
            ? null
            : new
            {
                version = result.Header.Version,
                headerSize = result.Header.HeaderSize,
                effectiveHeaderSize = result.Header.EffectiveHeaderSize,
                uncompressedSize = result.Header.UncompressedSize,
                blockSize = result.Header.BlockSize,
                sectorCount = result.Header.SectorCount,
                indexShift = result.Header.IndexShift,
                indexEntryCount = result.Header.IndexEntryCount,
                indexTableSizeBytes = result.Header.IndexTableSizeBytes
            };

        object[] issues = CreateIssuePayloads(result.Issues);
        CsoOperationIssueData? firstIssue = result.Issues.Count > 0 ? result.Issues[0] : null;

        if (result.Deep)
        {
            object deep = new
            {
                blocksChecked = result.BlocksChecked,
                bytesReconstructed = result.BytesReconstructed,
                sha256 = result.Sha256
            };

            JsonConsole.Write(new
            {
                schemaVersion = 1,
                command = "verify",
                success = operation.Success,
                input = SafeFullPath(options.InputPath),
                output = (string?)null,
                format = result.DetectedFormat,
                warnings = Array.Empty<string>(),
                diagnostics = new
                {
                    mode = "deep",
                    header,
                    deep,
                    issues
                },
                mode = "deep",
                header,
                deep,
                issues,
                error = operation.Success
                    ? null
                    : new CsoCommandError(
                        firstIssue?.Code ?? operation.ErrorCode ?? "VerificationFailed",
                        firstIssue?.Message ?? operation.Status)
            });
        }
        else
        {
            object? index = result.Header is null
                ? null
                : new
                {
                    entriesRead = result.EntriesRead,
                    expectedEntries = result.ExpectedEntries
                };

            JsonConsole.Write(new
            {
                schemaVersion = 1,
                command = "verify",
                success = operation.Success,
                input = SafeFullPath(options.InputPath),
                output = (string?)null,
                format = result.DetectedFormat,
                warnings = Array.Empty<string>(),
                diagnostics = new
                {
                    header,
                    index,
                    issues
                },
                header,
                index,
                issues,
                error = operation.Success
                    ? null
                    : new CsoCommandError(
                        firstIssue?.Code ?? operation.ErrorCode ?? "VerificationFailed",
                        firstIssue?.Message ?? operation.Status)
            });
        }

        return operation.Success
            ? CliExitCodes.Success
            : ToExitCode(firstIssue?.Code ?? operation.ErrorCode);
    }

    private static int WriteText(
        VerifyCommandOptions options,
        CsoOperationResult operation,
        CsoVerifyOperationData result)
    {
        Console.WriteLine(result.Deep ? "Deep Verification" : "CSO Verification");
        Console.WriteLine(result.Deep ? "-----------------" : "----------------");
        Console.WriteLine($"Input:  {SafeFullPath(options.InputPath)}");

        if (!string.IsNullOrWhiteSpace(result.DetectedFormat))
        {
            Console.WriteLine($"Format: {result.DetectedFormat}");
        }

        if (operation.Success)
        {
            Console.WriteLine("Status: OK");

            if (result.Deep)
            {
                Console.WriteLine($"Blocks checked:      {result.BlocksChecked:N0}");
                Console.WriteLine($"Bytes reconstructed: {result.BytesReconstructed:N0}");

                if (!string.IsNullOrWhiteSpace(result.Sha256))
                {
                    Console.WriteLine($"SHA256:              {result.Sha256}");
                }
            }
            else if (result.Header is not null)
            {
                Console.WriteLine($"Version:       {result.Header.Version}");
                Console.WriteLine($"Sectors:       {result.Header.SectorCount:N0}");
                Console.WriteLine($"Index entries: {result.EntriesRead:N0}");
            }

            return CliExitCodes.Success;
        }

        Console.Error.WriteLine("Status: FAILED");

        foreach (CsoOperationIssueData issue in result.Issues)
        {
            Console.Error.WriteLine($"{issue.Code}: {issue.Message}");
        }

        return ToExitCode(result.Issues.Count > 0 ? result.Issues[0].Code : operation.ErrorCode);
    }

    private static object[] CreateIssuePayloads(IReadOnlyList<CsoOperationIssueData> issues)
    {
        object[] payloads = new object[issues.Count];

        for (int index = 0; index < issues.Count; index++)
        {
            CsoOperationIssueData issue = issues[index];

            payloads[index] = new
            {
                code = issue.Code,
                message = issue.Message,
                blockIndex = issue.BlockIndex,
                offset = issue.Offset,
                expected = issue.Expected,
                actual = issue.Actual
            };
        }

        return payloads;
    }

    private static bool TryParseArgs(
        string[] args,
        out VerifyCommandOptions options)
    {
        options = default!;

        if (args.Length < 1)
        {
            return false;
        }

        string inputPath = args[0];
        bool json = false;
        bool deep = false;
        bool sha256 = false;

        for (int index = 1; index < args.Length; index++)
        {
            string arg = args[index];

            if (string.Equals(arg, "--json", StringComparison.OrdinalIgnoreCase))
            {
                json = true;
                continue;
            }

            if (string.Equals(arg, "--deep", StringComparison.OrdinalIgnoreCase))
            {
                deep = true;
                continue;
            }

            if (string.Equals(arg, "--sha256", StringComparison.OrdinalIgnoreCase))
            {
                sha256 = true;
                continue;
            }

            return false;
        }

        if (string.IsNullOrWhiteSpace(inputPath) || sha256 && !deep)
        {
            return false;
        }

        options = new VerifyCommandOptions(inputPath, json, deep, sha256);
        return true;
    }

    private static int ToExitCode(string? errorCode)
    {
        return errorCode switch
        {
            "InputNotFound" => CliExitCodes.InputNotFound,
            "InvalidMagic" or "HeaderTooSmall" or "InvalidHeaderSize" or "InvalidUncompressedSize" or "InvalidBlockSize" or "BlockSizeTooLarge" or "InvalidIndexShift"
                => CliExitCodes.InvalidCsoHeader,
            "UnsupportedVersion" or "UnsupportedCsoVersion" or "UnsupportedContainer"
                => CliExitCodes.UnsupportedCsoVersion,
            "IndexTableTruncated" or "IndexEntryTruncated" or "IndexOffsetsNotMonotonic" or "IndexOffsetPastEndOfFile" or "FinalOffsetPastEndOfFile" or "FirstDataOffsetBeforeIndexEnd" or "IndexEntryCountMismatch" or "FinalIndexEntryHasFlag" or "CsoV2FinalSentinelHighBit" or "FinalOffsetMismatch"
                => CliExitCodes.CorruptIndexTable,
            "CorruptCompressedBlock" or "CsoDeepVerifyFailed" or "InvalidCompressedBlockSize" or "StoredBlockTooSmall" or "UnexpectedEndOfFile" or "ReconstructedSizeMismatch" or "ReDumpRequired"
                => CliExitCodes.DecompressionFailed,
            "OperationCanceled" => CliExitCodes.OperationCanceled,
            _ => CliExitCodes.GeneralFailure
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
        Console.Error.WriteLine("Usage: csokit verify <input.iso|input.cso|input.zso|input.dax> [--deep] [--sha256] [--json]");
    }

    private sealed record VerifyCommandOptions(
        string InputPath,
        bool Json,
        bool Deep,
        bool Sha256);
}
