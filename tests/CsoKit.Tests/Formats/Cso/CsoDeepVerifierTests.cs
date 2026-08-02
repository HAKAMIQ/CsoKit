using System.Security.Cryptography;
using CsoKit.Core.Formats.Cso;

namespace CsoKit.Tests.Formats.Cso;

public sealed class CsoDeepVerifierTests
{
    [Fact]
    public void Verify_WithValidCso_ComputesReconstructedSha256()
    {
        byte[] original = CreateModuloBytes(4096 + 17, 251);
        string csoPath = CsoTestFileFactory.CreateTempCsoV1(original);

        try
        {
            CsoDeepVerifyResult result = new CsoDeepVerifier().Verify(csoPath, computeSha256: true);

            Assert.True(result.Success);
            Assert.Equal((ulong)original.Length, result.BytesReconstructed);
            Assert.Equal(Convert.ToHexString(SHA256.HashData(original)).ToLowerInvariant(), result.Sha256);
        }
        finally
        {
            File.Delete(csoPath);
        }
    }

    [Fact]
    public void Verify_WithCorruptCompressedBlock_ReturnsReDumpDiagnosis()
    {
        byte[] original = new byte[4096];
        string csoPath = CsoTestFileFactory.CreateTempCsoV1(original);

        try
        {
            CsoVerificationResult shallow = new CsoVerifier().Verify(csoPath);
            Assert.True(shallow.Success);
            Assert.True(shallow.Entries.Count > 1);

            using (FileStream output = new(csoPath, FileMode.Open, FileAccess.Write, FileShare.None))
            {
                output.Position = checked((long)shallow.Entries[0].Offset);
                output.Write([0xFF, 0xFF, 0xFF, 0xFF]);
            }

            CsoDeepVerifyResult result = new CsoDeepVerifier().Verify(csoPath, computeSha256: false);

            Assert.False(result.Success);
            Assert.Contains(result.Issues, issue => issue.Code == "CorruptCompressedBlock");
            Assert.Contains(result.Issues, issue => issue.Message.Contains("Re-dump required", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
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
}