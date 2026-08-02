using CsoKit.Core.Native;

namespace CsoKit.Cli.Commands;

internal static class NativeInfoCommand
{
    public static int Run()
    {
        NativeCsoRuntimeInfo info = NativeCsoRuntime.GetInfo();
        NativeCsoCapabilities capabilities = NativeCsoRuntime.GetCapabilities();

        Console.WriteLine($"Backend: {info.BackendName}");
        Console.WriteLine($"Native available: {info.IsAvailable}");

        if (!string.IsNullOrWhiteSpace(info.NativeVersion))
        {
            Console.WriteLine($"Native version: {info.NativeVersion}");
        }

        if (!string.IsNullOrWhiteSpace(info.FailureReason))
        {
            Console.WriteLine($"Fallback reason: {info.FailureReason}");
        }

        Console.WriteLine("Codec capabilities:");
        Console.WriteLine("  Managed Deflate: available");
        Console.WriteLine($"  Native zlib: {Availability(capabilities.HasZlib)}");
        Console.WriteLine($"  Native libdeflate: {Availability(capabilities.HasLibDeflate)}");
        Console.WriteLine($"  Native Zopfli: {Availability(capabilities.HasZopfli)}");
        Console.WriteLine($"  Native 7z-deflate: {SevenZipDeflateAvailability(capabilities.HasSevenZipDeflate)}");
        Console.WriteLine("  Managed LZ4 decode: available");
        Console.WriteLine($"  Native LZ4 decode: {Availability(capabilities.HasLz4)}");
        Console.WriteLine("  LZ4 encode: unavailable");

        return CliExitCodes.Success;
    }

    private static string Availability(bool available)
    {
        return available ? "available" : "unavailable";
    }

    private static string SevenZipDeflateAvailability(bool available)
    {
        return available ? "hidden" : "unavailable";
    }
}