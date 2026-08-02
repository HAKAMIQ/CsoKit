using System.Security.Cryptography;
using CsoKit.Core.Formats.Containers;
using CsoKit.Core.Formats.Cso;
using CsoKit.Tests.Fixtures;

namespace CsoKit.Tests.Formats.Cso;

public sealed class ContainerDeepVerifierTests
{
    [Fact]
    public void Verify_WithCso2Lz4_DoesNotUseCso1StoredFlagSemantics()
    {
        byte[] original = CreateModuloBytes(4096, 251, invert: false);
        string path = CsoTestFileFactory.CreateTempCso2(original);

        try
        {
            using Cso2ContainerReader reader = new(path);
            CsoDeepVerifyResult result = ContainerDeepVerifier.Verify(reader, computeSha256: true);

            Assert.True(result.Success, FormatIssues(result.Issues));
            Assert.Equal((ulong)original.Length, result.BytesReconstructed);
            Assert.Equal(Convert.ToHexString(SHA256.HashData(original)).ToLowerInvariant(), result.Sha256);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Verify_WithZsoLz4_DecodesEveryBlock()
    {
        byte[] original = CreateModuloBytes(4096, 251, invert: true);
        string path = CsoTestFileFactory.CreateTempZso(original);

        try
        {
            using ZsoContainerReader reader = new(path);
            CsoDeepVerifyResult result = ContainerDeepVerifier.Verify(reader, computeSha256: true);

            Assert.True(result.Success, FormatIssues(result.Issues));
            Assert.Equal((ulong)original.Length, result.BytesReconstructed);
            Assert.Equal(Convert.ToHexString(SHA256.HashData(original)).ToLowerInvariant(), result.Sha256);
        }
        finally
        {
            File.Delete(path);
        }
    }


    [Fact]
    public void Verify_WithRawIso_ReadsEverySector()
    {
        string path = ContainerFixtures.CreateMinimalIso9660();

        try
        {
            using IsoContainerReader reader = new(path);
            CsoDeepVerifyResult result = ContainerDeepVerifier.Verify(reader, computeSha256: true);

            Assert.True(result.Success, FormatIssues(result.Issues));
            Assert.Equal((ulong)new FileInfo(path).Length, result.BytesReconstructed);
            Assert.Equal(reader.BlockCount, result.BlocksChecked);
            Assert.Equal(reader.BlockCount, result.StoredBlocks);
            Assert.Equal(0, result.CompressedBlocks);
            Assert.Equal("Hybrid raw ISO verification", result.AlgorithmName);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void CsoDeepVerifier_WithCso2_ReturnsUnsupportedContainerInsteadOfMisreadingFlag()
    {
        byte[] original = new byte[4096];
        string path = CsoTestFileFactory.CreateTempCso2(original);

        try
        {
            CsoDeepVerifyResult result = new CsoDeepVerifier().Verify(path, computeSha256: false);

            Assert.False(result.Success);
            Assert.Contains(result.Issues, issue => issue.Code == "UnsupportedContainer");
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static byte[] CreateModuloBytes(int length, int modulo, bool invert)
    {
        byte[] bytes = new byte[length];

        for (int index = 0; index < bytes.Length; index++)
        {
            int value = index % modulo;
            bytes[index] = (byte)(invert ? 255 - value : value);
        }

        return bytes;
    }

    private static string FormatIssues(IReadOnlyList<CsoDeepVerifyIssue> issues)
    {
        if (issues.Count == 0)
        {
            return string.Empty;
        }

        string[] lines = new string[issues.Count];

        for (int index = 0; index < issues.Count; index++)
        {
            CsoDeepVerifyIssue issue = issues[index];
            lines[index] = $"{issue.Code}: {issue.Message}";
        }

        return string.Join(Environment.NewLine, lines);
    }
}