using CsoKit.Core.Formats.Cso;

namespace CsoKit.Tests.Formats.Cso;

public sealed class CsoDecompressorTests
{
    [Fact]
    public void Decompress_WithValidRawDeflateCsoV1_WritesExpectedIsoBytes()
    {
        byte[] original = CreateModuloBytes(8192 + 123, 4);

        string csoPath = CsoTestFileFactory.CreateTempCsoV1(original);
        string outputPath = CreateTempIsoPath();

        try
        {
            CsoDecompressor decompressor = new();
            CsoDecompressResult result = decompressor.Decompress(
                new CsoDecompressOptions(csoPath, outputPath, ForceOverwrite: false));

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal((ulong)original.Length, result.BytesWritten);
            Assert.Equal(original, File.ReadAllBytes(outputPath));
        }
        finally
        {
            File.Delete(csoPath);
            File.Delete(outputPath);
        }
    }

    [Fact]
    public void Decompress_WithLegacyVersionZeroCsoV1_WritesExpectedIsoBytes()
    {
        byte[] original = CreateModuloBytes(4096, 2);

        string csoPath = CsoTestFileFactory.CreateTempCsoV1(original, version: 0);
        string outputPath = CreateTempIsoPath();

        try
        {
            CsoDecompressor decompressor = new();
            CsoDecompressResult result = decompressor.Decompress(
                new CsoDecompressOptions(csoPath, outputPath, ForceOverwrite: false));

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal(original, File.ReadAllBytes(outputPath));
        }
        finally
        {
            File.Delete(csoPath);
            File.Delete(outputPath);
        }
    }

    [Fact]
    public void Decompress_WithUncompressedHighBitBlocks_WritesExpectedIsoBytes()
    {
        byte[] original = CreateModuloBytes(5000, 251);

        string csoPath = CsoTestFileFactory.CreateTempCsoV1(
            original,
            storeBlockUncompressed: static _ => true);

        string outputPath = CreateTempIsoPath();

        try
        {
            CsoDecompressor decompressor = new();
            CsoDecompressResult result = decompressor.Decompress(
                new CsoDecompressOptions(csoPath, outputPath, ForceOverwrite: false));

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal(original, File.ReadAllBytes(outputPath));
        }
        finally
        {
            File.Delete(csoPath);
            File.Delete(outputPath);
        }
    }

    [Fact]
    public void Decompress_WithForceOverwrite_ReplacesExistingOutput()
    {
        byte[] original = CreateModuloBytes(4096, 7);

        string csoPath = CsoTestFileFactory.CreateTempCsoV1(original);
        string outputPath = CreateTempIsoPath();
        File.WriteAllBytes(outputPath, [0xFF]);

        try
        {
            CsoDecompressor decompressor = new();
            CsoDecompressResult result = decompressor.Decompress(
                new CsoDecompressOptions(csoPath, outputPath, ForceOverwrite: true));

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal(original, File.ReadAllBytes(outputPath));
        }
        finally
        {
            File.Delete(csoPath);
            File.Delete(outputPath);
        }
    }

    [Fact]
    public void Decompress_WhenSiblingTmpFileExists_DoesNotDeleteIt()
    {
        byte[] original = CreateModuloBytes(4096, 3);

        string csoPath = CsoTestFileFactory.CreateTempCsoV1(original);
        string outputPath = CreateTempIsoPath();
        string siblingTempPath = outputPath + ".tmp";
        byte[] siblingTempBytes = [1, 2, 3, 4];
        File.WriteAllBytes(siblingTempPath, siblingTempBytes);

        try
        {
            CsoDecompressor decompressor = new();
            CsoDecompressResult result = decompressor.Decompress(
                new CsoDecompressOptions(csoPath, outputPath, ForceOverwrite: false));

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal(original, File.ReadAllBytes(outputPath));
            Assert.True(File.Exists(siblingTempPath));
            Assert.Equal(siblingTempBytes, File.ReadAllBytes(siblingTempPath));
        }
        finally
        {
            File.Delete(csoPath);
            File.Delete(outputPath);
            File.Delete(siblingTempPath);
        }
    }

    [Fact]
    public void Decompress_WithMissingOutputDirectory_DoesNotCreateDirectory()
    {
        byte[] original = CreateModuloBytes(4096, 5);

        string csoPath = CsoTestFileFactory.CreateTempCsoV1(original);
        string outputDirectory = Path.Combine(Path.GetTempPath(), $"CsoKit_Missing_{Guid.NewGuid():N}");
        string outputPath = Path.Combine(outputDirectory, "Game.iso");

        try
        {
            CsoDecompressor decompressor = new();
            CsoDecompressResult result = decompressor.Decompress(
                new CsoDecompressOptions(csoPath, outputPath, ForceOverwrite: false));

            Assert.False(result.Success);
            Assert.Equal("OutputDirectoryNotFound", result.ErrorCode);
            Assert.False(Directory.Exists(outputDirectory));
        }
        finally
        {
            File.Delete(csoPath);

            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
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

    private static string CreateTempIsoPath()
    {
        string fileName = Guid.NewGuid().ToString("N", System.Globalization.CultureInfo.InvariantCulture)[..8];
        return Path.Combine(Path.GetTempPath(), fileName + ".iso");
    }
}