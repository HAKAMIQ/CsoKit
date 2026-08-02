using System.Reflection;
using CsoKit.Core.Formats.Cso;

namespace CsoKit.Cli.Commands;

public static class CsoCommandDispatcher
{
    public static int Run(string[] args)
    {
        if (args.Length == 0)
        {
            PrintHelp();
            return CliExitCodes.InvalidArguments;
        }

        string command = args[0].Trim().ToLowerInvariant();

        return command switch
        {
            "detect" => DetectCommand.Run(args[1..]),
            "analyze" => AnalyzeCommand.Run(args[1..]),
            "info" => InfoCommand.Run(args[1..]),
            "verify" => VerifyCommand.Run(args[1..]),
            "repair" => RepairCommand.Run(args[1..]),
            "decompress" => DecompressCommand.Run(args[1..]),
            "compress" => CompressCommand.Run(args[1..]),
            "codecs" => RunNoArgumentCommand(args, CodecsCommand.Run),
            "native-info" => RunNoArgumentCommand(args, NativeInfoCommand.Run),
            "--help" or "-h" or "help" => PrintHelpAndReturnSuccess(),
            "--version" or "-v" => PrintVersionAndReturnSuccess(),
            _ => UnknownCommand(command)
        };
    }

    private static int RunNoArgumentCommand(
        string[] args,
        Func<int> command)
    {
        if (args.Length != 1)
        {
            Console.Error.WriteLine($"Command '{args[0]}' does not accept arguments.");
            PrintHelp();
            return CliExitCodes.InvalidArguments;
        }

        return command();
    }

    private static int UnknownCommand(string command)
    {
        Console.Error.WriteLine($"Unknown command: {command}");
        PrintHelp();
        return CliExitCodes.InvalidArguments;
    }

    private static int PrintHelpAndReturnSuccess()
    {
        PrintHelp();
        return CliExitCodes.Success;
    }

    private static int PrintVersionAndReturnSuccess()
    {
        string version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
            ?? "unknown";

        Console.WriteLine($"CsoKit {version}");
        return CliExitCodes.Success;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("CsoKit");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  csokit detect <input> [--json]");
        Console.WriteLine("  csokit analyze <input.iso> [--psp] [--allow-padding] [--json]");
        Console.WriteLine("  csokit info <input.cso> [--json]");
        Console.WriteLine("  csokit verify <input.cso|input.zso|input.dax> [--deep] [--sha256] [--json]");
        Console.WriteLine("  csokit repair <input.iso|input.cso> -o <output.cso> [--profile game-safe] [--repair pad-last-sector] [--deep-verify] [--codec-report] [--codec-report-block-limit <n>] [--force] [--json]");
        Console.WriteLine("  csokit decompress <input.cso> [-o <output.iso>] [--force] [--quiet] [--json]");
        Console.WriteLine($"  csokit compress <input.iso> [-o <output.cso>] [--profile <{CsoCompressionProfilePolicy.SupportedNamesText}>] [--fast] [--threads <n>] [--block <bytes>] [--zopfli] [--deep-verify] [--codec-report] [--codec-report-block-limit <n>] [--force] [--quiet] [--json]");
        Console.WriteLine($"  csokit compress <input.iso> --measure [--profile <{CsoCompressionProfilePolicy.SupportedNamesText}>] [--fast] [--block <bytes>] [--zopfli] [--quiet] [--json]");
        Console.WriteLine("  csokit codecs");
        Console.WriteLine("  csokit native-info");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  detect       Detect ISO/CSO/ZSO/DAX intake format.");
        Console.WriteLine("  analyze      Validate PSP ISO structure without modifying it.");
        Console.WriteLine("  info         Read and print CSO header information.");
        Console.WriteLine("  verify       Validate CSO header/index, or every block with --deep.");
        Console.WriteLine("  repair       Rebuild readable ISO/CSO/ZSO/DAX input into game-safe CSO1.");
        Console.WriteLine("  decompress   Decompress CSO to ISO.");
        Console.WriteLine("  compress     Compress ISO to CSO.");
        Console.WriteLine("  codecs       Show codec matrix and native availability.");
        Console.WriteLine("  native-info  Show native backend availability.");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  csokit detect game.iso");
        Console.WriteLine("  csokit analyze game.iso --psp");
        Console.WriteLine("  csokit compress game.iso --profile game-safe --deep-verify");
        Console.WriteLine("  csokit verify game.cso --deep --sha256");
        Console.WriteLine("  csokit repair old.zso -o fixed.cso --deep-verify");
        Console.WriteLine("  csokit codecs");
        Console.WriteLine("  csokit native-info");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --help       Show help.");
        Console.WriteLine("  --version    Show version.");
    }
}