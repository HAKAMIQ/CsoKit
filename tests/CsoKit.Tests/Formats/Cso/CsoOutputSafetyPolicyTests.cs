using CsoKit.Core.Formats.Cso;

namespace CsoKit.Tests.Formats.Cso;

public sealed class CsoOutputSafetyPolicyTests
{
    [Fact]
    public void Validate_WithSameInputAndOutput_ReturnsFailure()
    {
        string path = Path.Combine(Path.GetTempPath(), $"CsoKit_{Guid.NewGuid():N}.cso");

        try
        {
            File.WriteAllBytes(path, [1, 2, 3]);

            CsoOutputSafetyPolicy policy = new();
            CsoOutputSafetyResult result = policy.Validate(path, path, forceOverwrite: true);

            Assert.False(result.Success);
            Assert.Equal("SameInputOutputPath", result.ErrorCode);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Validate_WithExistingOutputAndNoForce_ReturnsOutputAlreadyExists()
    {
        string inputPath = Path.Combine(Path.GetTempPath(), $"CsoKit_Input_{Guid.NewGuid():N}.cso");
        string outputName = Guid.NewGuid().ToString("N", System.Globalization.CultureInfo.InvariantCulture)[..8] + ".iso";
        string outputPath = Path.Combine(Path.GetTempPath(), outputName);

        try
        {
            File.WriteAllBytes(inputPath, [1]);
            File.WriteAllBytes(outputPath, [2]);

            CsoOutputSafetyPolicy policy = new();
            CsoOutputSafetyResult result = policy.Validate(inputPath, outputPath, forceOverwrite: false);

            Assert.False(result.Success);
            Assert.Equal("OutputAlreadyExists", result.ErrorCode);
        }
        finally
        {
            File.Delete(inputPath);
            File.Delete(outputPath);
        }
    }

    [Fact]
    public void Validate_WithExistingOutputAndForce_ReturnsSuccess()
    {
        string inputPath = Path.Combine(Path.GetTempPath(), $"CsoKit_Input_{Guid.NewGuid():N}.cso");
        string outputName = Guid.NewGuid().ToString("N", System.Globalization.CultureInfo.InvariantCulture)[..8] + ".iso";
        string outputPath = Path.Combine(Path.GetTempPath(), outputName);

        try
        {
            File.WriteAllBytes(inputPath, [1]);
            File.WriteAllBytes(outputPath, [2]);

            CsoOutputSafetyPolicy policy = new();
            CsoOutputSafetyResult result = policy.Validate(inputPath, outputPath, forceOverwrite: true);

            Assert.True(result.Success);
        }
        finally
        {
            File.Delete(inputPath);
            File.Delete(outputPath);
        }
    }

    [Fact]
    public void Validate_WithDirectoryAsOutput_ReturnsOutputPathIsDirectory()
    {
        string inputPath = Path.Combine(Path.GetTempPath(), $"CsoKit_Input_{Guid.NewGuid():N}.cso");
        string outputDirectory = Path.Combine(Path.GetTempPath(), $"CsoKit_OutputDir_{Guid.NewGuid():N}");

        try
        {
            File.WriteAllBytes(inputPath, [1]);
            Directory.CreateDirectory(outputDirectory);

            CsoOutputSafetyPolicy policy = new();
            CsoOutputSafetyResult result = policy.Validate(inputPath, outputDirectory, forceOverwrite: true);

            Assert.False(result.Success);
            Assert.Equal("OutputPathIsDirectory", result.ErrorCode);
        }
        finally
        {
            File.Delete(inputPath);

            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void Validate_WithMissingOutputDirectory_ReturnsOutputDirectoryNotFound()
    {
        string inputPath = Path.Combine(Path.GetTempPath(), $"CsoKit_Input_{Guid.NewGuid():N}.iso");
        string outputDirectory = Path.Combine(Path.GetTempPath(), $"CsoKit_MissingDir_{Guid.NewGuid():N}");
        string outputPath = Path.Combine(outputDirectory, "Game.cso");

        try
        {
            File.WriteAllBytes(inputPath, [1]);

            CsoOutputSafetyPolicy policy = new();
            CsoOutputSafetyResult result = policy.Validate(inputPath, outputPath, forceOverwrite: false);

            Assert.False(result.Success);
            Assert.Equal("OutputDirectoryNotFound", result.ErrorCode);
            Assert.False(Directory.Exists(outputDirectory));
        }
        finally
        {
            File.Delete(inputPath);
        }
    }

    [Theory]
    [InlineData("A.cso", "OutputFileNameTooShort")]
    [InlineData("ABCDEFGHIJK.cso", "OutputFileNameTooLong")]
    public void Validate_WithInvalidOutputFileNameLength_ReturnsFailure(
        string outputFileName,
        string expectedErrorCode)
    {
        string directory = Path.Combine(Path.GetTempPath(), $"CsoKit_NamePolicy_{Guid.NewGuid():N}");
        string inputPath = Path.Combine(directory, "input.iso");
        string outputPath = Path.Combine(directory, outputFileName);

        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(inputPath, [1]);

            CsoOutputSafetyResult result = new CsoOutputSafetyPolicy().Validate(
                inputPath,
                outputPath,
                forceOverwrite: false);

            Assert.False(result.Success);
            Assert.Equal(expectedErrorCode, result.ErrorCode);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

}
