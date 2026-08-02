using CsoKit.Core.Formats.DiscImage;
using CsoKit.Tests.Formats.Cso;

namespace CsoKit.Tests.Formats.DiscImage;

public sealed class FormatDetectorTests
{
    [Fact]
    public void Detect_WithCsoV1_ReturnsCso1Metadata()
    {
        byte[] original = new byte[4096];
        string csoPath = CsoTestFileFactory.CreateTempCsoV1(original);

        try
        {
            FormatDetectionResult result = FormatDetector.Detect(csoPath);

            Assert.True(result.Success);
            Assert.Equal(DetectedDiscFormat.Cso1, result.Format);
            Assert.Equal("CISO", result.Magic);
            Assert.Equal((ulong)original.Length, result.UncompressedSize);
            Assert.Equal(2048U, result.BlockSize);
            Assert.Equal(2, result.SectorCount);
        }
        finally
        {
            File.Delete(csoPath);
        }
    }

    [Fact]
    public void Detect_WithIso9660PrimaryVolumeDescriptor_ReturnsRawIso()
    {
        string isoPath = CreateTempPath(".iso");

        try
        {
            byte[] iso = new byte[17 * 2048];
            int pvd = 16 * 2048;
            iso[pvd] = 1;
            "CD001"u8.CopyTo(iso.AsSpan(pvd + 1));
            File.WriteAllBytes(isoPath, iso);

            FormatDetectionResult result = FormatDetector.Detect(isoPath);

            Assert.True(result.Success);
            Assert.Equal(DetectedDiscFormat.RawIso, result.Format);
            Assert.Equal("ISO9660", result.Magic);
            Assert.Equal(2048U, result.BlockSize);
        }
        finally
        {
            File.Delete(isoPath);
        }
    }

    [Theory]
    [InlineData("ZISO", DetectedDiscFormat.Zso)]
    [InlineData("DAX\0", DetectedDiscFormat.Dax)]
    public void Detect_WithIntakeContainerMagic_ReturnsContainerFormat(
        string magic,
        DetectedDiscFormat expectedFormat)
    {
        string path = CreateTempPath(".bin");

        try
        {
            byte[] bytes = new byte[64];
            System.Text.Encoding.ASCII.GetBytes(magic).CopyTo(bytes, 0);
            File.WriteAllBytes(path, bytes);

            FormatDetectionResult result = FormatDetector.Detect(path);

            Assert.True(result.Success);
            Assert.Equal(expectedFormat, result.Format);
            Assert.NotEmpty(result.Warnings);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string CreateTempPath(string extension)
    {
        return Path.Combine(Path.GetTempPath(), $"CsoKit_{Guid.NewGuid():N}{extension}");
    }
}