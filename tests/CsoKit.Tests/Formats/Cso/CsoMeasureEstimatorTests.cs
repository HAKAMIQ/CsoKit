using CsoKit.Core.Formats.Cso;

namespace CsoKit.Tests.Formats.Cso;

public sealed class CsoMeasureEstimatorTests
{
    [Fact]
    public void Measure_WithValidIso_ReturnsEstimateWithoutCreatingOutputFile()
    {
        byte[] original = CreateModuloBytes(8192 + 123, 251);

        string isoPath = CreateTempPath(".iso");
        string csoPath = CreateTempPath(".cso");

        try
        {
            File.WriteAllBytes(isoPath, original);

            CsoMeasureEstimator estimator = new();
            CsoMeasureResult result = estimator.Measure(new CsoMeasureOptions(isoPath));

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal((ulong)original.Length, result.OriginalBytes);
            Assert.True(result.EstimatedBytes > 0);
            Assert.Equal(result.TotalBlocks, result.CompressedBlocks + result.StoredBlocks);
            Assert.False(File.Exists(csoPath));
        }
        finally
        {
            File.Delete(isoPath);
            File.Delete(csoPath);
        }
    }

    [Fact]
    public void Measure_WithValidIso_MatchesCurrentCompressorOutputSizeAndBlockDecisions()
    {
        byte[] original = CreateModuloBytes(16384 + 333, 17);

        string isoPath = CreateTempPath(".iso");
        string csoPath = CreateTempPath(".cso");

        try
        {
            File.WriteAllBytes(isoPath, original);

            CsoMeasureEstimator estimator = new();
            CsoMeasureResult measureResult = estimator.Measure(new CsoMeasureOptions(isoPath));

            CsoCompressor compressor = new();
            CsoCompressResult compressResult = compressor.Compress(
                new CsoCompressOptions(isoPath, csoPath, ForceOverwrite: false));

            Assert.True(measureResult.Success, measureResult.ErrorMessage);
            Assert.True(compressResult.Success, compressResult.ErrorMessage);
            Assert.Equal(compressResult.BytesRead, measureResult.OriginalBytes);
            Assert.Equal(compressResult.BytesWritten, measureResult.EstimatedBytes);
            Assert.Equal(compressResult.CompressedBlocks, measureResult.CompressedBlocks);
            Assert.Equal(compressResult.StoredBlocks, measureResult.StoredBlocks);
        }
        finally
        {
            File.Delete(isoPath);
            File.Delete(csoPath);
        }
    }

    [Fact]
    public void Measure_WithMissingInput_ReturnsInputNotFound()
    {
        string isoPath = CreateTempPath(".iso");

        CsoMeasureEstimator estimator = new();
        CsoMeasureResult result = estimator.Measure(new CsoMeasureOptions(isoPath));

        Assert.False(result.Success);
        Assert.Equal("InputNotFound", result.ErrorCode);
        Assert.Equal((ulong)0, result.OriginalBytes);
        Assert.Equal((ulong)0, result.EstimatedBytes);
    }

    [Fact]
    public void Measure_WithEmptyInput_ReturnsInvalidInputSize()
    {
        string isoPath = CreateTempPath(".iso");

        try
        {
            File.WriteAllBytes(isoPath, []);

            CsoMeasureEstimator estimator = new();
            CsoMeasureResult result = estimator.Measure(new CsoMeasureOptions(isoPath));

            Assert.False(result.Success);
            Assert.Equal("InvalidInputSize", result.ErrorCode);
        }
        finally
        {
            File.Delete(isoPath);
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

    private static string CreateTempPath(string extension)
    {
        string fileName = Guid.NewGuid().ToString("N", System.Globalization.CultureInfo.InvariantCulture)[..8];
        return Path.Combine(Path.GetTempPath(), fileName + Path.GetExtension(extension));
    }
}