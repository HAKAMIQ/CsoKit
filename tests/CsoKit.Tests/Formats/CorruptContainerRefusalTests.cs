using CsoKit.Core.Formats.Cso;
using CsoKit.Tests.Fixtures;

namespace CsoKit.Tests.Formats;

public sealed class CorruptContainerRefusalTests
{
    private static readonly string[] CorruptRepairErrorCodes =
    [
        "ReDumpRequired",
        "CorruptCompressedBlock",
        "IoError",
    ];

    [Fact]
    public void Verify_WithCorruptIndex_DoesNotSucceed()
    {
        byte[] logical = ContainerFixtures.DeterministicBytes(4096);
        string input = ContainerFixtures.CreateCso1Compressed(logical);

        try
        {
            byte[] bytes = File.ReadAllBytes(input);
            bytes[24] = 0xFF;
            bytes[25] = 0xFF;
            bytes[26] = 0xFF;
            bytes[27] = 0x7F;
            File.WriteAllBytes(input, bytes);

            CsoVerificationResult result = new CsoVerifier().Verify(input);

            Assert.False(result.Success);
            Assert.Contains(result.Issues, issue =>
                issue.Code is "IndexOffsetPastEndOfFile" or "IndexOffsetsNotMonotonic" or "FinalOffsetPastEndOfFile");
        }
        finally
        {
            File.Delete(input);
        }
    }

    [Fact]
    public void VerifyDeep_WithTruncatedPayload_Fails()
    {
        byte[] logical = ContainerFixtures.DeterministicBytes(4096);
        string good = ContainerFixtures.CreateCso1Compressed(logical);
        string truncated = ContainerFixtures.CreateTruncatedCopy(good, bytesToRemove: 16);

        try
        {
            CsoDeepVerifyResult verify = new CsoDeepVerifier().Verify(
                truncated,
                computeSha256: false);

            Assert.False(verify.Success);
        }
        finally
        {
            File.Delete(good);
            File.Delete(truncated);
        }
    }

    [Fact]
    public void Repair_WithCorruptInput_DoesNotLeaveOutput()
    {
        byte[] logical = ContainerFixtures.DeterministicBytes(4096);
        string good = ContainerFixtures.CreateZso(logical);
        string corrupt = ContainerFixtures.CreateTruncatedCopy(good, bytesToRemove: 12);
        string output = ContainerFixtures.TempPath(".cso");

        try
        {
            CsoRepairResult repair = CsoRepairer.Repair(new CsoRepairOptions(
                corrupt,
                output,
                ForceOverwrite: false,
                Profile: CsoCompressionProfile.GameSafe,
                PadLastSector: false,
                DeepVerify: true));

            Assert.False(repair.Success);
            Assert.False(File.Exists(output));
            Assert.Contains(repair.ErrorCode, CorruptRepairErrorCodes);
        }
        finally
        {
            File.Delete(good);
            File.Delete(corrupt);
            File.Delete(output);
        }
    }
}