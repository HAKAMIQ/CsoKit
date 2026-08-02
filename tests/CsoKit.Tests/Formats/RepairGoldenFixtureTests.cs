using CsoKit.Core.Formats.Cso;
using CsoKit.Tests.Fixtures;

namespace CsoKit.Tests.Formats;

public sealed class RepairGoldenFixtureTests
{
    [Theory]
    [InlineData("cso1")]
    [InlineData("zso")]
    [InlineData("dax")]
    [InlineData("cso2")]
    public void RepairContainer_StreamsToGameSafeCso1WithoutTempIso(string fixture)
    {
        byte[] logical = ContainerFixtures.DeterministicBytes(8192);
        string input = fixture switch
        {
            "cso1" => ContainerFixtures.CreateCso1Mixed(logical),
            "zso" => ContainerFixtures.CreateZso(logical),
            "dax" => ContainerFixtures.CreateDaxCompressed(logical),
            "cso2" => ContainerFixtures.CreateCso2Lz4(logical),
            _ => throw new ArgumentOutOfRangeException(nameof(fixture), fixture, "Unknown fixture."),
        };
        string output = ContainerFixtures.TempPath(".cso");

        try
        {
            CsoRepairResult repair = CsoRepairer.Repair(new CsoRepairOptions(
                input,
                output,
                ForceOverwrite: false,
                Profile: CsoCompressionProfile.GameSafe,
                PadLastSector: false,
                DeepVerify: true));

            Assert.True(repair.Success, repair.ErrorMessage);
            Assert.Equal("Streaming", repair.Mode);
            Assert.False(repair.UsedTempIso);
            Assert.True(File.Exists(output));
            Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(output)!, $".{Path.GetFileName(output)}.*.repair.iso"));

            CsoDeepVerifyResult verify = new CsoDeepVerifier().Verify(output, computeSha256: false);
            Assert.True(verify.Success);
            Assert.Equal((ulong)logical.Length, verify.BytesReconstructed);
        }
        finally
        {
            File.Delete(input);
            File.Delete(output);
        }
    }
}