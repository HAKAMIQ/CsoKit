using CsoKit.Core.Native;

namespace CsoKit.Tests.Native;

public sealed class NativeLibrarySearchPolicyTests
{
    [Fact]
    public void ProductionSearch_UsesOnlyApplicationBaseDirectory()
    {
        string? previous = Environment.GetEnvironmentVariable(
            NativeCsoRuntime.EnableDevelopmentSearchEnvironmentVariable);

        try
        {
            Environment.SetEnvironmentVariable(
                NativeCsoRuntime.EnableDevelopmentSearchEnvironmentVariable,
                null);

            string[] candidates = [.. NativeCsoRuntime.EnumerateNativeLibraryCandidates()];

            Assert.Single(candidates);
            Assert.Equal(
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, GetLibraryFileName())),
                candidates[0]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                NativeCsoRuntime.EnableDevelopmentSearchEnvironmentVariable,
                previous);
        }
    }

    private static string GetLibraryFileName()
    {
        return OperatingSystem.IsWindows()
            ? "CsoKit.Native.dll"
            : OperatingSystem.IsMacOS()
                ? "libCsoKit.Native.dylib"
                : "libCsoKit.Native.so";
    }
}
