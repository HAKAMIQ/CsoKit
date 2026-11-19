using CsoKit.Core.Native;

namespace CsoKit.Cli.Commands;

internal static class NativeBackendWarning
{
    /// <summary>
    /// Compression silently falls back to managed Deflate when CsoKit.Native.dll is
    /// missing or rejected, which produces larger output than the release build.
    /// </summary>
    public static void WriteIfUnavailable()
    {
        NativeCsoRuntimeInfo info = NativeCsoRuntime.GetInfo();

        if (info.IsAvailable)
        {
            return;
        }

        Console.Error.WriteLine(
            $"Warning: native backend unavailable ({(info.FailureReason ?? "unknown reason").TrimEnd('.')}). " +
            "Compressing with managed Deflate; output may be larger. Keep CsoKit.Native.dll beside csokit.exe.");
    }
}
