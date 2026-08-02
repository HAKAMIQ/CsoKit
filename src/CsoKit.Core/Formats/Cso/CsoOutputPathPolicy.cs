using System.Diagnostics.CodeAnalysis;

namespace CsoKit.Core.Formats.Cso;

public sealed class CsoOutputPathPolicy
{
    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "Keep the instance API stable for existing CLI callers.")]
    public string CreateCompressionOutputPath(string inputPath)
    {
        return CreateSiblingOutputPath(inputPath, ".cso");
    }

    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "Keep the instance API stable for existing CLI callers.")]
    public string CreateDecompressionOutputPath(string inputPath)
    {
        return CreateSiblingOutputPath(inputPath, ".iso");
    }

    private static string CreateSiblingOutputPath(
        string inputPath,
        string outputExtension)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            throw new ArgumentException("Input path is empty.", nameof(inputPath));
        }

        string fullInputPath = Path.GetFullPath(inputPath);
        string directory = Path.GetDirectoryName(fullInputPath) ?? Directory.GetCurrentDirectory();
        string baseName = Path.GetFileNameWithoutExtension(fullInputPath);

        return CsoFileNamePolicy.CreateUniquePath(
            directory,
            baseName,
            outputExtension,
            preferredSuffix: string.Empty);
    }
}