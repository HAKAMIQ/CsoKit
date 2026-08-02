using CsoKit.Core.Formats.Containers;
using CsoKit.Core.Formats.Cso;
using CsoKit.Core.Formats.DiscImage;
using CsoKit.Tests.Fixtures;

namespace CsoKit.Tests.Formats;

public sealed class GoldenContainerTests
{
    [Theory]
    [InlineData("cso1-compressed")]
    [InlineData("cso1-stored")]
    [InlineData("cso1-mixed")]
    [InlineData("cso1-last-sector")]
    [InlineData("cso2-deflate")]
    [InlineData("cso2-lz4")]
    [InlineData("zso-lz4")]
    [InlineData("dax-zlib")]
    [InlineData("dax-stored-area")]
    public void GoldenContainer_ReadsLogicalBytes(string fixture)
    {
        byte[] logical = fixture == "cso1-last-sector"
            ? ContainerFixtures.DeterministicBytes(3073)
            : ContainerFixtures.DeterministicBytes(8192);
        string path = CreateFixture(fixture, logical);

        try
        {
            using IBlockContainerReader reader = CreateReader(path, FormatDetector.Detect(path).Format);
            byte[] actual = ReadAll(reader);
            Assert.Equal(logical, actual);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void UnknownRandomInput_DetectFailsWithoutContainerClaim()
    {
        string path = ContainerFixtures.CreateRandomInput();

        try
        {
            FormatDetectionResult result = FormatDetector.Detect(path);

            Assert.True(result.Success);
            Assert.Equal(DetectedDiscFormat.Unknown, result.Format);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void IsoMinimal_DetectsRawIso()
    {
        string path = ContainerFixtures.CreateMinimalIso9660();

        try
        {
            FormatDetectionResult result = FormatDetector.Detect(path);

            Assert.True(result.Success);
            Assert.Equal(DetectedDiscFormat.RawIso, result.Format);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string CreateFixture(string fixture, byte[] logical)
    {
        return fixture switch
        {
            "cso1-compressed" => ContainerFixtures.CreateCso1Compressed(logical),
            "cso1-stored" => ContainerFixtures.CreateCso1Stored(logical),
            "cso1-mixed" => ContainerFixtures.CreateCso1Mixed(logical),
            "cso1-last-sector" => ContainerFixtures.CreateCso1Compressed(logical),
            "cso2-deflate" => ContainerFixtures.CreateCso2Deflate(logical),
            "cso2-lz4" => ContainerFixtures.CreateCso2Lz4(logical),
            "zso-lz4" => ContainerFixtures.CreateZso(logical),
            "dax-zlib" => ContainerFixtures.CreateDaxCompressed(logical),
            "dax-stored-area" => ContainerFixtures.CreateDaxWithNonCompressedArea(logical),
            _ => throw new ArgumentOutOfRangeException(nameof(fixture), fixture, "Unknown fixture."),
        };
    }

    private static IBlockContainerReader CreateReader(string path, DetectedDiscFormat format)
    {
        return format switch
        {
            DetectedDiscFormat.Cso1 => new Cso1ContainerReader(path),
            DetectedDiscFormat.Cso2 => new Cso2ContainerReader(path),
            DetectedDiscFormat.Zso => new ZsoContainerReader(path),
            DetectedDiscFormat.Dax => new DaxContainerReader(path),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported fixture format."),
        };
    }

    private static byte[] ReadAll(IBlockContainerReader reader)
    {
        byte[] output = new byte[checked((int)reader.UncompressedSize)];
        byte[] block = new byte[checked((int)reader.BlockSize)];
        int offset = 0;

        for (int index = 0; index < reader.BlockCount; index++)
        {
            int read = reader.ReadBlock(index, block);
            block.AsSpan(0, read).CopyTo(output.AsSpan(offset));
            offset += read;
        }

        return output;
    }
}