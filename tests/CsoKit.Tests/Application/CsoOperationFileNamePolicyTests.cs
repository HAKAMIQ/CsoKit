using CsoKit.Application;

namespace CsoKit.Tests.Application;

public sealed class CsoOperationFileNamePolicyTests
{
    [Theory]
    [InlineData("A.cso")]
    [InlineData("ABCDEFGHIJK.iso")]
    public void TryValidateOutputFileName_WithInvalidLength_ReturnsFalse(string outputPath)
    {
        bool valid = CsoOperationService.TryValidateOutputFileName(
            outputPath,
            out string? errorMessage);

        Assert.False(valid);
        Assert.False(string.IsNullOrWhiteSpace(errorMessage));
    }

    [Fact]
    public void CreateSuggestedRepairOutputPath_KeepsBaseNameWithinTenCharacters()
    {
        string inputPath = Path.Combine(Path.GetTempPath(), "VeryLongGameName.iso");

        string outputPath = CsoOperationService.CreateSuggestedRepairOutputPath(inputPath);
        string baseName = Path.GetFileNameWithoutExtension(outputPath);

        Assert.InRange(baseName.Length, 2, 10);
        Assert.EndsWith("-r", baseName, StringComparison.Ordinal);
    }
}
