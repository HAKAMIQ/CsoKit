using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace CsoKit.Core.Native;

public static class NativeCsoRuntime
{
    public const string DisableNativeEnvironmentVariable = "CSOKIT_DISABLE_NATIVE";
    public const string EnableDevelopmentSearchEnvironmentVariable = "CSOKIT_NATIVE_DEV_SEARCH";
    public const string ExpectedNativeSha256EnvironmentVariable = "CSOKIT_NATIVE_SHA256";
    public const uint SupportedAbiVersion = 2;

    private const string LibraryName = "CsoKit.Native";
    private const int NativeStatusOk = 0;
    private const int NativeStatusCodecUnavailable = 4;

    private static readonly Lazy<NativeValidationState> ValidationState = new(
        ValidateNativeRuntime,
        LazyThreadSafetyMode.ExecutionAndPublication);

    private static bool resolverInstalled;

    static NativeCsoRuntime()
    {
        try
        {
            NativeLibrary.SetDllImportResolver(
                typeof(NativeCsoRuntime).Assembly,
                ResolveNativeLibrary);
            resolverInstalled = true;
        }
        catch (InvalidOperationException)
        {
            resolverInstalled = false;
        }
    }

    public static NativeCsoRuntimeInfo GetInfo()
    {
        if (IsDisabledByEnvironment())
        {
            return CreateManagedFallback($"Native backend disabled by {DisableNativeEnvironmentVariable}.");
        }

        NativeValidationState state = ValidationState.Value;

        return state.IsAvailable
            ? new NativeCsoRuntimeInfo(
                IsAvailable: true,
                BackendName: "native",
                NativeVersion: state.VersionText,
                FailureReason: null)
            : CreateManagedFallback(state.FailureReason ?? "Native runtime validation failed.");
    }

    public static bool TryDeflateZopfli(
        ReadOnlySpan<byte> input,
        int iterations,
        out byte[] compressed)
    {
        compressed = [];

        if (input.IsEmpty ||
            iterations < 1 ||
            iterations > 100 ||
            !CanUseNativeCodecs())
        {
            return false;
        }

        byte[] inputBuffer = input.ToArray();
        byte[] outputBuffer = new byte[inputBuffer.Length];

        try
        {
            int result = NativeMethods.csokit_native_deflate_zopfli(
                inputBuffer,
                new UIntPtr((uint)inputBuffer.Length),
                iterations,
                outputBuffer,
                new UIntPtr((uint)outputBuffer.Length),
                out UIntPtr outputSize);

            return TryFinalizeCompressedBuffer(result, outputSize, ref outputBuffer, out compressed);
        }
        catch (Exception ex) when (IsNativeLoadException(ex))
        {
            return false;
        }
    }

    public static bool TryDeflateRaw(
        NativeCsoRawCodec codec,
        int level,
        int strategy,
        ReadOnlySpan<byte> input,
        out byte[] compressed)
    {
        compressed = [];

        if (input.IsEmpty || !CanUseNativeCodecs())
        {
            return false;
        }

        byte[] inputBuffer = input.ToArray();
        byte[] outputBuffer = new byte[inputBuffer.Length];

        try
        {
            int result = NativeMethods.csokit_native_deflate_raw(
                (int)codec,
                level,
                strategy,
                inputBuffer,
                new UIntPtr((uint)inputBuffer.Length),
                outputBuffer,
                new UIntPtr((uint)outputBuffer.Length),
                out UIntPtr outputSize);

            return TryFinalizeCompressedBuffer(result, outputSize, ref outputBuffer, out compressed);
        }
        catch (Exception ex) when (IsNativeLoadException(ex))
        {
            return false;
        }
    }

    public static bool TryInflateRaw(
        ReadOnlySpan<byte> compressed,
        int expectedBytes,
        out byte[] restored)
    {
        restored = [];

        if (compressed.IsEmpty ||
            expectedBytes <= 0 ||
            !CanUseNativeCodecs())
        {
            return false;
        }

        byte[] inputBuffer = compressed.ToArray();
        byte[] outputBuffer = new byte[expectedBytes];

        try
        {
            int result = NativeMethods.csokit_native_inflate_raw(
                inputBuffer,
                new UIntPtr((uint)inputBuffer.Length),
                outputBuffer,
                new UIntPtr((uint)outputBuffer.Length),
                out UIntPtr outputSize);

            if (result == NativeStatusCodecUnavailable || result != NativeStatusOk)
            {
                return false;
            }

            if (outputSize.ToUInt64() != (ulong)expectedBytes)
            {
                return false;
            }

            restored = outputBuffer;
            return true;
        }
        catch (Exception ex) when (IsNativeLoadException(ex))
        {
            return false;
        }
    }

    public static NativeCsoCapabilities GetCapabilities()
    {
        if (IsDisabledByEnvironment())
        {
            return NativeCsoCapabilities.ManagedFallback;
        }

        NativeValidationState state = ValidationState.Value;

        if (!state.IsAvailable)
        {
            return NativeCsoCapabilities.ManagedFallback;
        }

        try
        {
            CsoKitNativeCapabilities capabilities = default;
            int result = NativeMethods.csokit_native_get_capabilities(ref capabilities);

            if (result != NativeStatusOk || capabilities.AbiVersion != SupportedAbiVersion)
            {
                return NativeCsoCapabilities.ManagedFallback;
            }

            return new NativeCsoCapabilities(
                capabilities.AbiVersion,
                capabilities.HasZlib != 0,
                capabilities.HasLibDeflate != 0,
                capabilities.HasZopfli != 0,
                capabilities.HasSevenZipDeflate != 0,
                capabilities.HasLz4 != 0);
        }
        catch (Exception ex) when (IsNativeLoadException(ex))
        {
            return NativeCsoCapabilities.ManagedFallback;
        }
    }

    public static bool IsDisabledByEnvironment()
    {
        return IsEnabledEnvironmentValue(DisableNativeEnvironmentVariable);
    }

    private static bool CanUseNativeCodecs()
    {
        return !IsDisabledByEnvironment() && ValidationState.Value.IsAvailable;
    }

    private static NativeValidationState ValidateNativeRuntime()
    {
        if (!resolverInstalled)
        {
            return NativeValidationState.Unavailable("The secure native library resolver could not be installed.");
        }

        try
        {
            int probe = NativeMethods.csokit_native_probe();

            if (probe != NativeStatusOk)
            {
                return NativeValidationState.Unavailable($"Native probe failed with status {probe}.");
            }

            CsoKitNativeVersion version = default;
            int versionResult = NativeMethods.csokit_native_get_version(ref version);

            if (versionResult != NativeStatusOk)
            {
                return NativeValidationState.Unavailable($"Native version query failed with status {versionResult}.");
            }

            if (version.AbiVersion != SupportedAbiVersion)
            {
                return NativeValidationState.Unavailable(
                    $"Native ABI {version.AbiVersion} is incompatible. Supported ABI is {SupportedAbiVersion}.");
            }

            string versionText = $"{version.Major}.{version.Minor}.{version.Patch} ABI {version.AbiVersion}";
            return NativeValidationState.Available(versionText);
        }
        catch (Exception ex) when (IsNativeLoadException(ex))
        {
            return NativeValidationState.Unavailable(ex.Message);
        }
    }

    private static bool TryFinalizeCompressedBuffer(
        int result,
        UIntPtr outputSize,
        ref byte[] outputBuffer,
        out byte[] compressed)
    {
        compressed = [];

        if (result != NativeStatusOk)
        {
            return false;
        }

        ulong rawOutputSize = outputSize.ToUInt64();

        if (rawOutputSize == 0 || rawOutputSize > (ulong)outputBuffer.Length)
        {
            return false;
        }

        int compressedLength = checked((int)rawOutputSize);

        if (compressedLength != outputBuffer.Length)
        {
            Array.Resize(ref outputBuffer, compressedLength);
        }

        compressed = outputBuffer;
        return true;
    }

    private static NativeCsoRuntimeInfo CreateManagedFallback(string reason)
    {
        return new NativeCsoRuntimeInfo(
            IsAvailable: false,
            BackendName: "managed",
            NativeVersion: null,
            FailureReason: reason);
    }

    private static IntPtr ResolveNativeLibrary(
        string libraryName,
        Assembly _,
        DllImportSearchPath? __)
    {
        if (!string.Equals(libraryName, LibraryName, StringComparison.Ordinal))
        {
            return IntPtr.Zero;
        }

        foreach (string candidate in EnumerateNativeLibraryCandidates())
        {
            if (!File.Exists(candidate) || !MatchesExpectedHash(candidate))
            {
                continue;
            }

            if (NativeLibrary.TryLoad(candidate, out IntPtr handle))
            {
                return handle;
            }
        }

        throw new DllNotFoundException(
            $"{GetNativeLibraryFileName()} was not found in an approved native library location.");
    }

    internal static IEnumerable<string> EnumerateNativeLibraryCandidates()
    {
        string fileName = GetNativeLibraryFileName();
        string applicationCandidate = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, fileName));

        yield return applicationCandidate;

        if (!IsEnabledEnvironmentValue(EnableDevelopmentSearchEnvironmentVariable))
        {
            yield break;
        }

        HashSet<string> visited = new(StringComparer.OrdinalIgnoreCase)
        {
            applicationCandidate,
        };

        foreach (string start in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
        {
            foreach (string root in EnumerateAncestorDirectories(start))
            {
                foreach (string configuration in new[] { "Release", "Debug" })
                {
                    string candidate = Path.GetFullPath(Path.Combine(
                        root,
                        "artifacts",
                        "native-build",
                        "win-x64",
                        configuration,
                        fileName));

                    if (visited.Add(candidate))
                    {
                        yield return candidate;
                    }
                }
            }
        }
    }

    private static string GetNativeLibraryFileName()
    {
        return OperatingSystem.IsWindows()
            ? $"{LibraryName}.dll"
            : OperatingSystem.IsMacOS()
                ? $"lib{LibraryName}.dylib"
                : $"lib{LibraryName}.so";
    }

    private static bool MatchesExpectedHash(string path)
    {
        string? expected = Environment.GetEnvironmentVariable(ExpectedNativeSha256EnvironmentVariable)?.Trim();

        if (string.IsNullOrWhiteSpace(expected))
        {
            return true;
        }

        if (expected.Length != 64 || expected.Any(character => !Uri.IsHexDigit(character)))
        {
            return false;
        }

        try
        {
            using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            string actual = Convert.ToHexString(SHA256.HashData(stream));
            return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static IEnumerable<string> EnumerateAncestorDirectories(string start)
    {
        DirectoryInfo? current;

        try
        {
            current = new DirectoryInfo(Path.GetFullPath(start));
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            yield break;
        }

        while (current is not null)
        {
            yield return current.FullName;
            current = current.Parent;
        }
    }

    private static bool IsEnabledEnvironmentValue(string variableName)
    {
        string? value = Environment.GetEnvironmentVariable(variableName)?.Trim();

        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsNativeLoadException(Exception exception)
    {
        return exception is DllNotFoundException or
            EntryPointNotFoundException or
            BadImageFormatException or
            InvalidOperationException;
    }

    private readonly record struct NativeValidationState(
        bool IsAvailable,
        string? VersionText,
        string? FailureReason)
    {
        public static NativeValidationState Available(string versionText) => new(true, versionText, null);

        public static NativeValidationState Unavailable(string reason) => new(false, null, reason);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CsoKitNativeVersion
    {
        public uint AbiVersion;
        public uint Major;
        public uint Minor;
        public uint Patch;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CsoKitNativeCapabilities
    {
        public uint AbiVersion;
        public uint HasZlib;
        public uint HasLibDeflate;
        public uint HasZopfli;
        public uint HasSevenZipDeflate;
        public uint HasLz4;
    }

#pragma warning disable SYSLIB1054
    private static class NativeMethods
    {
        [DllImport(LibraryName, ExactSpelling = true)]
        internal static extern int csokit_native_probe();

        [DllImport(LibraryName, ExactSpelling = true)]
        internal static extern int csokit_native_get_version(
            ref CsoKitNativeVersion version);

        [DllImport(LibraryName, ExactSpelling = true)]
        internal static extern int csokit_native_get_capabilities(
            ref CsoKitNativeCapabilities capabilities);

        [DllImport(LibraryName, ExactSpelling = true)]
        internal static extern int csokit_native_deflate_raw(
            int codec,
            int level,
            int strategy,
            byte[] input,
            UIntPtr inputSize,
            byte[] output,
            UIntPtr outputCapacity,
            out UIntPtr outputSize);

        [DllImport(LibraryName, ExactSpelling = true)]
        internal static extern int csokit_native_inflate_raw(
            byte[] input,
            UIntPtr inputSize,
            byte[] output,
            UIntPtr outputCapacity,
            out UIntPtr outputSize);

        [DllImport(LibraryName, ExactSpelling = true)]
        internal static extern int csokit_native_deflate_zopfli(
            byte[] input,
            UIntPtr inputSize,
            int iterations,
            byte[] output,
            UIntPtr outputCapacity,
            out UIntPtr outputSize);
    }
#pragma warning restore SYSLIB1054
}
