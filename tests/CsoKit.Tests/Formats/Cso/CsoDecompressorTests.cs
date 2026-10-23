using System.Buffers.Binary;
using CsoKit.Core.Formats.Cso;

namespace CsoKit.Tests.Formats.Cso;

public sealed class CsoDecompressorTests
{
    [Theory]
    [InlineData(false, "valid")]
    [InlineData(true, "valid")]
    [InlineData(false, "excess-output")]
    [InlineData(true, "excess-output")]
    [InlineData(false, "truncated")]
    [InlineData(true, "truncated")]
    [InlineData(false, "appended")]
    [InlineData(true, "appended")]
    [InlineData(false, "concatenated")]
    [InlineData(true, "concatenated")]
    [InlineData(false, "padding")]
    [InlineData(true, "padding")]
    [InlineData(false, "excess-padding")]
    [InlineData(true, "excess-padding")]
    [InlineData(false, "unused-bits")]
    public void Decompress_CompressedStreamBoundaries_RespectLengthEndAndAlignment(bool zlib, string variant)
    {
        // A non-final stored block followed by a final empty fixed-Huffman block.
        // Removing its last byte still leaves all 2048 decoded bytes available.
        int decodedLength = variant == "excess-output" ? 2049 : 2048;
        byte[] raw = new byte[5 + decodedLength + 2];
        BinaryPrimitives.WriteUInt16LittleEndian(raw.AsSpan(1), (ushort)decodedLength);
        BinaryPrimitives.WriteUInt16LittleEndian(raw.AsSpan(3), (ushort)~decodedLength);
        raw[^2] = 3;
        if (variant == "unused-bits") raw[^1] = 0xFC;
        byte[] payload = zlib ? [0x78, 0x01, .. raw, 0x08, (byte)(decodedLength - 2048), 0x00, 0x01] : raw;
        if (variant == "truncated") payload = payload[..^1];
        if (variant == "appended") payload = [.. payload, 0xA5];
        if (variant == "concatenated") payload = [.. payload, .. payload];
        byte shift = variant is "padding" or "excess-padding" ? (byte)4 : (byte)0;
        int padding = shift == 0 ? 0 : (16 - payload.Length % 16) % 16;
        if (variant == "excess-padding") padding += 16;
        // Padding bytes have no defined value; only their alignment extent matters.
        payload = [.. payload, .. Enumerable.Repeat((byte)0xA5, padding)];
        byte[] cso = new byte[32 + payload.Length];
        "CISO"u8.CopyTo(cso);
        BinaryPrimitives.WriteUInt32LittleEndian(cso.AsSpan(4), 24);
        BinaryPrimitives.WriteUInt64LittleEndian(cso.AsSpan(8), 2048);
        BinaryPrimitives.WriteUInt32LittleEndian(cso.AsSpan(16), 2048);
        cso[20] = 1;
        cso[21] = shift;
        BinaryPrimitives.WriteUInt32LittleEndian(cso.AsSpan(24), 32u >> shift);
        BinaryPrimitives.WriteUInt32LittleEndian(cso.AsSpan(28), (uint)cso.Length >> shift);
        payload.CopyTo(cso, 32);
        string directory = CreateTempDirectory();
        string input = Path.Combine(directory, "input.cso");
        string output = Path.Combine(directory, "Game.iso");
        File.WriteAllBytes(input, cso);
        try
        {
            CsoDecompressResult result = new CsoDecompressor().Decompress(new(input, output, false));
            bool valid = variant is "valid" or "padding" or "unused-bits";
            Assert.Equal(valid, result.Success);
            if (valid)
            {
                Assert.Equal(new byte[2048], File.ReadAllBytes(output));
                Assert.Equal(2048UL, result.BytesWritten);
            }
            else
            {
                Assert.Equal("InvalidCompressedData", result.ErrorCode);
                Assert.False(File.Exists(output));
            }
            Assert.Equal(valid ? 2 : 1, Directory.GetFiles(directory).Length);
            Assert.Equal(cso, File.ReadAllBytes(input));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Conversion_WhenProgressThrowsUnexpectedException_CleansOwnedTempAndPreservesDestination(bool compress)
    {
        string directory = CreateTempDirectory();
        string input = compress ? Path.Combine(directory, "input.iso") : CsoTestFileFactory.CreateTempCsoV1(new byte[4096]);
        string output = Path.Combine(directory, compress ? "Game.cso" : "Game.iso");
        byte[] existing = [1, 2, 3, 4];
        byte[] sibling = [7, 8, 9];
        File.WriteAllBytes(output, existing);
        File.WriteAllBytes(output + ".tmp", sibling);
        if (compress) File.WriteAllBytes(input, new byte[4096]);
        var unexpected = new InvalidOperationException("progress failure");
        try
        {
            Exception thrown = Assert.Throws<InvalidOperationException>(() =>
            {
                if (compress)
                    new CsoCompressor().Compress(new(input, output, true, Progress: new ThrowingProgress<CsoCompressProgress>(unexpected, p => p.CompletedBlocks > 0)));
                else
                    new CsoDecompressor().Decompress(new(input, output, true, new ThrowingProgress<CsoDecompressProgress>(unexpected, p => p.CompletedBlocks > 0)));
            });
            Assert.Same(unexpected, thrown);
            Assert.Equal(existing, File.ReadAllBytes(output));
            Assert.Equal(sibling, File.ReadAllBytes(output + ".tmp"));
            Assert.Equal(compress ? 3 : 2, Directory.GetFiles(directory).Length);
        }
        finally
        {
            File.Delete(input);
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class ThrowingProgress<T>(Exception error, Func<T, bool> shouldThrow) : IProgress<T>
    {
        public void Report(T value)
        {
            if (shouldThrow(value)) throw error;
        }
    }

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

    [Fact]
    public void Decompress_WithStoredBlockSmallerThanBlockSize_FailsWithoutLeavingOutput()
    {
        byte[] original = CreateModuloBytes(2048 * 3, 251);

        string csoPath = CsoTestFileFactory.CreateTempCsoV1(
            original,
            storeBlockUncompressed: static _ => true);

        // Shrink stored block 0 by one byte in the index: block 1 then starts one byte early.
        byte[] csoBytes = File.ReadAllBytes(csoPath);
        int entryOffset = CsoConstants.MinimumHeaderSize + sizeof(uint);
        uint rawEntry = BinaryPrimitives.ReadUInt32LittleEndian(csoBytes.AsSpan(entryOffset, sizeof(uint)));
        BinaryPrimitives.WriteUInt32LittleEndian(csoBytes.AsSpan(entryOffset, sizeof(uint)), rawEntry - 1);
        File.WriteAllBytes(csoPath, csoBytes);

        string outputDirectory = CreateTempDirectory();
        string outputPath = Path.Combine(outputDirectory, "Game.iso");

        try
        {
            CsoDecompressor decompressor = new();
            CsoDecompressResult result = decompressor.Decompress(
                new CsoDecompressOptions(csoPath, outputPath, ForceOverwrite: false));

            Assert.False(result.Success);
            Assert.Equal("InvalidCompressedData", result.ErrorCode);
            Assert.Contains("block 0", result.ErrorMessage, StringComparison.Ordinal);
            Assert.Empty(Directory.EnumerateFileSystemEntries(outputDirectory));
        }
        finally
        {
            File.Delete(csoPath);
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Fact]
    public void Decompress_WithFinalOffsetNotMatchingFileLength_Fails()
    {
        byte[] original = CreateModuloBytes(4096 + 100, 7);

        string csoPath = CsoTestFileFactory.CreateTempCsoV1(original);

        using (FileStream stream = new(csoPath, FileMode.Append, FileAccess.Write))
        {
            stream.Write(new byte[16]);
        }

        string outputDirectory = CreateTempDirectory();
        string outputPath = Path.Combine(outputDirectory, "Game.iso");

        try
        {
            CsoDecompressor decompressor = new();
            CsoDecompressResult result = decompressor.Decompress(
                new CsoDecompressOptions(csoPath, outputPath, ForceOverwrite: false));

            Assert.False(result.Success);
            Assert.Equal("InvalidCompressedData", result.ErrorCode);
            Assert.Empty(Directory.EnumerateFileSystemEntries(outputDirectory));
        }
        finally
        {
            File.Delete(csoPath);
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Fact]
    public void Decompress_WithUncompressedSizeNearUInt64Max_ReturnsInvalidHeaderValues()
    {
        string csoPath = CsoTestFileFactory.CreateTempCsoV1(CreateModuloBytes(4096, 3));

        byte[] csoBytes = File.ReadAllBytes(csoPath);
        BinaryPrimitives.WriteUInt64LittleEndian(csoBytes.AsSpan(8, 8), ulong.MaxValue - 1);
        File.WriteAllBytes(csoPath, csoBytes);

        string outputDirectory = CreateTempDirectory();
        string outputPath = Path.Combine(outputDirectory, "Game.iso");

        try
        {
            CsoDecompressor decompressor = new();
            CsoDecompressResult result = decompressor.Decompress(
                new CsoDecompressOptions(csoPath, outputPath, ForceOverwrite: false));

            Assert.False(result.Success);
            Assert.Equal("InvalidHeaderValues", result.ErrorCode);
            Assert.Empty(Directory.EnumerateFileSystemEntries(outputDirectory));
        }
        finally
        {
            File.Delete(csoPath);
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"CsoKit_Out_{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
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
