using CsoKit.Core.Formats.Cso;
using CsoKit.Core.Native;

namespace CsoKit.Tests.Formats.Cso;

public sealed class CsoCompressorTests
{
    [Fact]
    public void Compress_WithValidIso_CreatesCsoThatDecompressesToOriginalBytes()
    {
        byte[] original = CreateModuloBytes(8192 + 123, 251);

        string isoPath = CreateTempPath(".iso");
        string csoPath = CreateTempPath(".cso");
        string outputIsoPath = CreateTempPath(".out.iso");

        try
        {
            File.WriteAllBytes(isoPath, original);

            CsoCompressor compressor = new();
            CsoCompressResult compressResult = compressor.Compress(
                new CsoCompressOptions(isoPath, csoPath, ForceOverwrite: false));

            Assert.True(compressResult.Success, compressResult.ErrorMessage);
            Assert.Equal((ulong)original.Length, compressResult.BytesRead);
            Assert.True(File.Exists(csoPath));

            CsoDecompressor decompressor = new();
            CsoDecompressResult decompressResult = decompressor.Decompress(
                new CsoDecompressOptions(csoPath, outputIsoPath, ForceOverwrite: false));

            Assert.True(decompressResult.Success, decompressResult.ErrorMessage);
            Assert.Equal(original, File.ReadAllBytes(outputIsoPath));
        }
        finally
        {
            File.Delete(isoPath);
            File.Delete(csoPath);
            File.Delete(outputIsoPath);
        }
    }

    [Fact]
    public void Compress_WithCompressibleIso_UsesCompressedBlocks()
    {
        byte[] original = new byte[8192];

        string isoPath = CreateTempPath(".iso");
        string csoPath = CreateTempPath(".cso");

        try
        {
            File.WriteAllBytes(isoPath, original);

            CsoCompressor compressor = new();
            CsoCompressResult result = compressor.Compress(
                new CsoCompressOptions(isoPath, csoPath, ForceOverwrite: false));

            Assert.True(result.Success, result.ErrorMessage);
            Assert.True(result.CompressedBlocks > 0);
            Assert.True(result.BytesWritten < result.BytesRead);
            Assert.Equal(result.CompressedBlocks + result.StoredBlocks, SumValues(result.EffectiveCodecWins));
            Assert.True(HasDeflateCodecWin(result.EffectiveCodecWins));
        }
        finally
        {
            File.Delete(isoPath);
            File.Delete(csoPath);
        }
    }

    [Fact]
    public void Compress_WithCodecReportLimit_RetainsBoundedBlockSampleAndFullSummary()
    {
        byte[] original = new byte[2048 * 5];

        string isoPath = CreateTempPath(".iso");
        string csoPath = CreateTempPath(".cso");

        try
        {
            File.WriteAllBytes(isoPath, original);

            CsoCompressor compressor = new();
            CsoCompressResult result = compressor.Compress(
                new CsoCompressOptions(
                    isoPath,
                    csoPath,
                    ForceOverwrite: false,
                    CollectCodecReport: true,
                    CodecReportBlockLimit: 2));

            Assert.True(result.Success, result.ErrorMessage);
            Assert.NotNull(result.CodecTrialSummary);
            Assert.Equal(5, result.CodecTrialSummary.BlocksReported);
            Assert.Equal(2, result.CodecTrialSummary.Blocks.Count);
            Assert.NotEmpty(result.CodecTrialSummary.CandidateAttempts);
            Assert.Equal(5, SumValues(result.CodecTrialSummary.SelectedCodecWins));
        }
        finally
        {
            File.Delete(isoPath);
            File.Delete(csoPath);
        }
    }

    [Fact]
    public void Compress_WithParallelWorkersAndLargerBlockSize_CreatesRoundtrippableCso()
    {
        byte[] original = CreateScaledModuloBytes(65536 + 123, 31, 251);

        string isoPath = CreateTempPath(".iso");
        string csoPath = CreateTempPath(".cso");
        string outputIsoPath = CreateTempPath(".out.iso");

        try
        {
            File.WriteAllBytes(isoPath, original);

            CsoCompressor compressor = new();
            CsoCompressResult compressResult = compressor.Compress(
                new CsoCompressOptions(
                    isoPath,
                    csoPath,
                    ForceOverwrite: false,
                    BlockSize: 4096,
                    WorkerCount: 4));

            Assert.True(compressResult.Success, compressResult.ErrorMessage);

            CsoHeaderReadResult headerResult = new CsoHeaderReader().Read(csoPath);
            Assert.True(headerResult.Success, headerResult.ErrorMessage);
            Assert.NotNull(headerResult.Header);
            Assert.Equal(4096U, headerResult.Header.BlockSize);

            CsoDecompressor decompressor = new();
            CsoDecompressResult decompressResult = decompressor.Decompress(
                new CsoDecompressOptions(csoPath, outputIsoPath, ForceOverwrite: false));

            Assert.True(decompressResult.Success, decompressResult.ErrorMessage);
            Assert.Equal(original, File.ReadAllBytes(outputIsoPath));
        }
        finally
        {
            File.Delete(isoPath);
            File.Delete(csoPath);
            File.Delete(outputIsoPath);
        }
    }

    [Fact]
    public void Compress_WithZopfliAndDisabledNative_ReturnsClearFailure()
    {
        string? previous = Environment.GetEnvironmentVariable(NativeCsoRuntime.DisableNativeEnvironmentVariable);
        string isoPath = CreateTempPath(".iso");
        string csoPath = CreateTempPath(".cso");

        try
        {
            Environment.SetEnvironmentVariable(NativeCsoRuntime.DisableNativeEnvironmentVariable, "1");
            File.WriteAllBytes(isoPath, new byte[4096]);

            CsoCompressor compressor = new();
            CsoCompressResult result = compressor.Compress(
                new CsoCompressOptions(
                    isoPath,
                    csoPath,
                    ForceOverwrite: false,
                    UseZopfli: true));

            Assert.False(result.Success);
            Assert.Equal("NativeZopfliUnavailable", result.ErrorCode);
            Assert.False(File.Exists(csoPath));
        }
        finally
        {
            Environment.SetEnvironmentVariable(NativeCsoRuntime.DisableNativeEnvironmentVariable, previous);
            File.Delete(isoPath);
            File.Delete(csoPath);
        }
    }

    [Fact]
    public void Compress_WhenOutputExistsWithoutForce_Fails()
    {
        string isoPath = CreateTempPath(".iso");
        string csoPath = CreateTempPath(".cso");

        try
        {
            File.WriteAllBytes(isoPath, [1, 2, 3, 4]);
            File.WriteAllBytes(csoPath, [9, 9, 9]);

            CsoCompressor compressor = new();
            CsoCompressResult result = compressor.Compress(
                new CsoCompressOptions(isoPath, csoPath, ForceOverwrite: false));

            Assert.False(result.Success);
            Assert.Equal("OutputAlreadyExists", result.ErrorCode);
            Assert.Equal([9, 9, 9], File.ReadAllBytes(csoPath));
        }
        finally
        {
            File.Delete(isoPath);
            File.Delete(csoPath);
        }
    }

    [Fact]
    public void Compress_WithForceOverwrite_ReplacesExistingOutput()
    {
        byte[] original = CreateModuloBytes(4096, 13);

        string isoPath = CreateTempPath(".iso");
        string csoPath = CreateTempPath(".cso");

        try
        {
            File.WriteAllBytes(isoPath, original);
            File.WriteAllBytes(csoPath, [9, 9, 9]);

            CsoCompressor compressor = new();
            CsoCompressResult result = compressor.Compress(
                new CsoCompressOptions(isoPath, csoPath, ForceOverwrite: true));

            Assert.True(result.Success, result.ErrorMessage);
            Assert.NotEqual([9, 9, 9], File.ReadAllBytes(csoPath));
        }
        finally
        {
            File.Delete(isoPath);
            File.Delete(csoPath);
        }
    }

    [Fact]
    public void Compress_WithMissingOutputDirectory_DoesNotCreateDirectory()
    {
        string isoPath = CreateTempPath(".iso");
        string outputDirectory = Path.Combine(Path.GetTempPath(), $"CsoKit_Missing_{Guid.NewGuid():N}");
        string csoPath = Path.Combine(outputDirectory, "Game.cso");

        try
        {
            File.WriteAllBytes(isoPath, [1, 2, 3, 4]);

            CsoCompressor compressor = new();
            CsoCompressResult result = compressor.Compress(
                new CsoCompressOptions(isoPath, csoPath, ForceOverwrite: false));

            Assert.False(result.Success);
            Assert.Equal("OutputDirectoryNotFound", result.ErrorCode);
            Assert.False(Directory.Exists(outputDirectory));
        }
        finally
        {
            File.Delete(isoPath);

            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void Compress_WithAllZeroIso_ReportsZeroBlocksAfterFullBlockScan()
    {
        byte[] original = new byte[4096];

        string isoPath = CreateTempPath(".iso");
        string csoPath = CreateTempPath(".cso");
        string outputIsoPath = CreateTempPath(".out.iso");

        try
        {
            File.WriteAllBytes(isoPath, original);

            CsoCompressor compressor = new();
            CsoCompressResult result = compressor.Compress(
                new CsoCompressOptions(
                    isoPath,
                    csoPath,
                    ForceOverwrite: false,
                    BlockSize: 2048,
                    DeepVerifyOutput: true,
                    CollectCodecReport: true,
                    CodecReportBlockLimit: 0));

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal(2, result.ZeroBlocks);
            Assert.Equal(result.CompressedBlocks + result.StoredBlocks, SumValues(result.EffectiveCodecWins));

            CsoDecompressor decompressor = new();
            CsoDecompressResult decompressResult = decompressor.Decompress(
                new CsoDecompressOptions(csoPath, outputIsoPath, ForceOverwrite: false));

            Assert.True(decompressResult.Success, decompressResult.ErrorMessage);
            Assert.Equal(original, File.ReadAllBytes(outputIsoPath));
        }
        finally
        {
            File.Delete(isoPath);
            File.Delete(csoPath);
            File.Delete(outputIsoPath);
        }
    }

    private static bool HasDeflateCodecWin(IReadOnlyDictionary<string, int> codecWins)
    {
        foreach (KeyValuePair<string, int> item in codecWins)
        {
            if (item.Key.StartsWith("managed-deflate", StringComparison.Ordinal) ||
                item.Key.StartsWith("native-", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static int SumValues(IReadOnlyDictionary<string, int> values)
    {
        int total = 0;

        foreach (KeyValuePair<string, int> item in values)
        {
            total = checked(total + item.Value);
        }

        return total;
    }


    [Fact]
    public void Compress_WithWorkerCountAbovePolicyLimit_FailsBeforeCreatingOutput()
    {
        string isoPath = CreateTempPath(".iso");
        string csoPath = CreateTempPath(".cso");

        try
        {
            File.WriteAllBytes(isoPath, new byte[4096]);
            int invalidWorkers = checked(CsoKit.Core.Compression.CsoWorkerPolicy.GetMaximumWorkerCount() + 1);

            CsoCompressResult result = new CsoCompressor().Compress(
                new CsoCompressOptions(
                    isoPath,
                    csoPath,
                    ForceOverwrite: false,
                    WorkerCount: invalidWorkers));

            Assert.False(result.Success);
            Assert.Equal("InvalidThreadCount", result.ErrorCode);
            Assert.False(File.Exists(csoPath));
        }
        finally
        {
            File.Delete(isoPath);
            File.Delete(csoPath);
        }
    }

    private static byte[] CreateModuloBytes(int length, int modulo)
    {
        byte[] bytes = new byte[length];

        for (int index = 0; index < bytes.Length; index++)
        {
            bytes[index] = (byte)(index % modulo);
        }

        return bytes;
    }

    private static byte[] CreateScaledModuloBytes(int length, int multiplier, int modulo)
    {
        byte[] bytes = new byte[length];

        for (int index = 0; index < bytes.Length; index++)
        {
            bytes[index] = (byte)((index * multiplier) % modulo);
        }

        return bytes;
    }

    private static string CreateTempPath(string extension)
    {
        string fileName = Guid.NewGuid().ToString("N", System.Globalization.CultureInfo.InvariantCulture)[..8];
        return Path.Combine(Path.GetTempPath(), fileName + Path.GetExtension(extension));
    }
}