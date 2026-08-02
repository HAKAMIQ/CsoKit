using System.Diagnostics.CodeAnalysis;

namespace CsoKit.Core.Formats.Cso;

public sealed class CsoOutputSafetyPolicy
{
    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "Keep the instance API stable for existing CLI/core callers.")]
    public CsoOutputSafetyResult Validate(
        string inputPath,
        string outputPath,
        bool forceOverwrite)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            return CsoOutputSafetyResult.Fail("InvalidInputPath", "Input path is empty.");
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return CsoOutputSafetyResult.Fail("InvalidOutputPath", "Output path is empty.");
        }

        string fullInputPath;
        string fullOutputPath;

        try
        {
            fullInputPath = Path.GetFullPath(inputPath);
            fullOutputPath = Path.GetFullPath(outputPath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return CsoOutputSafetyResult.Fail("InvalidOutputPath", ex.Message);
        }

        if (string.Equals(fullInputPath, fullOutputPath, StringComparison.OrdinalIgnoreCase))
        {
            return CsoOutputSafetyResult.Fail(
                "SameInputOutputPath",
                "Input and output paths must not point to the same file.");
        }

        if (Directory.Exists(fullOutputPath))
        {
            return CsoOutputSafetyResult.Fail(
                "OutputPathIsDirectory",
                "Output path points to an existing directory.");
        }

        if (!CsoFileNamePolicy.TryValidateOutputPath(
                fullOutputPath,
                out string? fileNameErrorCode,
                out string? fileNameErrorMessage))
        {
            return CsoOutputSafetyResult.Fail(
                fileNameErrorCode ?? "InvalidOutputPath",
                fileNameErrorMessage ?? "Output file name is invalid.");
        }

        string? outputDirectory = Path.GetDirectoryName(fullOutputPath);

        if (!string.IsNullOrWhiteSpace(outputDirectory) &&
            !Directory.Exists(outputDirectory))
        {
            return CsoOutputSafetyResult.Fail(
                "OutputDirectoryNotFound",
                "Output directory was not found. CsoKit does not create output folders automatically.");
        }

        if (File.Exists(fullOutputPath) && !forceOverwrite)
        {
            return CsoOutputSafetyResult.Fail(
                "OutputAlreadyExists",
                "Output file already exists. Use --force to overwrite.");
        }

        return CsoOutputSafetyResult.Ok();
    }
}