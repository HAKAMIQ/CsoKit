using System.Buffers.Binary;
using CsoKit.Core.Formats.Cso;

namespace CsoKit.Tests.Formats.Cso;

public sealed class CsoVerifierTests
{
    [Fact]
    public void Verify_WithValidCsoLikeFile_ReturnsSuccess()
    {
        string path = CreateTempCsoLikeFile(
            uncompressedSize: 4096,
            blockSize: 2048,
            version: 1,
            indexShift: 0,
            rawEntries: [36, 100, 200],
            dataLength: 200);

        try
        {
            CsoVerifier verifier = new();
            CsoVerificationResult result = verifier.Verify(path);

            Assert.True(result.Success);
            Assert.Empty(result.Issues);
            Assert.NotNull(result.Header);
            Assert.Equal(3, result.Entries.Count);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Verify_WithNonMonotonicOffsets_ReturnsFailure()
    {
        string path = CreateTempCsoLikeFile(
            uncompressedSize: 4096,
            blockSize: 2048,
            version: 1,
            indexShift: 0,
            rawEntries: [36, 120, 90],
            dataLength: 200);

        try
        {
            CsoVerifier verifier = new();
            CsoVerificationResult result = verifier.Verify(path);

            Assert.False(result.Success);
            Assert.Contains(result.Issues, issue => issue.Code == "IndexOffsetsNotMonotonic");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Verify_WithIndexOffsetPastEndOfFile_ReturnsFailure()
    {
        string path = CreateTempCsoLikeFile(
            uncompressedSize: 4096,
            blockSize: 2048,
            version: 1,
            indexShift: 0,
            rawEntries: [36, 500, 500],
            dataLength: 200);

        try
        {
            CsoVerifier verifier = new();
            CsoVerificationResult result = verifier.Verify(path);

            Assert.False(result.Success);
            Assert.Contains(result.Issues, issue => issue.Code == "IndexOffsetPastEndOfFile");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Verify_WithFinalOffsetPastEndOfFile_ReturnsFailure()
    {
        string path = CreateTempCsoLikeFile(
            uncompressedSize: 4096,
            blockSize: 2048,
            version: 1,
            indexShift: 0,
            rawEntries: [36, 100, 500],
            dataLength: 200);

        try
        {
            CsoVerifier verifier = new();
            CsoVerificationResult result = verifier.Verify(path);

            Assert.False(result.Success);
            Assert.Contains(result.Issues, issue => issue.Code == "FinalOffsetPastEndOfFile");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Verify_WithCso2FinalSentinelHighBit_ReturnsFailure()
    {
        string path = CreateTempCsoLikeFile(
            uncompressedSize: 4096,
            blockSize: 2048,
            version: 2,
            indexShift: 0,
            rawEntries: [36, 100, 0x800000C8],
            dataLength: 200);

        try
        {
            CsoVerifier verifier = new();
            CsoVerificationResult result = verifier.Verify(path);

            Assert.False(result.Success);
            Assert.Contains(result.Issues, issue => issue.Code == "CsoV2FinalSentinelHighBit");
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string CreateTempCsoLikeFile(
        ulong uncompressedSize,
        uint blockSize,
        byte version,
        byte indexShift,
        uint[] rawEntries,
        int dataLength)
    {
        int indexSize = rawEntries.Length * sizeof(uint);
        int totalLength = Math.Max(CsoConstants.MinimumHeaderSize + indexSize, dataLength);
        byte[] bytes = new byte[totalLength];

        bytes[0] = (byte)'C';
        bytes[1] = (byte)'I';
        bytes[2] = (byte)'S';
        bytes[3] = (byte)'O';

        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4, 4), 24);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(8, 8), uncompressedSize);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16, 4), blockSize);

        bytes[20] = version;
        bytes[21] = indexShift;
        bytes[22] = 0;
        bytes[23] = 0;

        int offset = CsoConstants.MinimumHeaderSize;

        foreach (uint rawEntry in rawEntries)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset, sizeof(uint)), rawEntry);
            offset += sizeof(uint);
        }

        string path = Path.Combine(Path.GetTempPath(), $"CsoKit_{Guid.NewGuid():N}.cso");
        File.WriteAllBytes(path, bytes);
        return path;
    }
}