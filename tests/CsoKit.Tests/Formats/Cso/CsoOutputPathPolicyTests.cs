using CsoKit.Core.Formats.Cso;

namespace CsoKit.Tests.Formats.Cso;

public sealed class CsoOutputPathPolicyTests
{
    [Fact]
    public void CreateCompressionOutputPath_WhenTargetIsAvailable_UsesSameFolderWithCsoExtension()
    {
        string directory = CreateTempDirectory();
        string isoPath = Path.Combine(directory, "Game.iso");

        try
        {
            File.WriteAllBytes(isoPath, [1]);

            CsoOutputPathPolicy policy = new();
            string outputPath = policy.CreateCompressionOutputPath(isoPath);

            Assert.Equal(Path.Combine(directory, "Game.cso"), outputPath);
            Assert.False(Directory.Exists(Path.Combine(directory, "_cso-output")));
            Assert.False(Directory.Exists(Path.Combine(directory, "output")));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void CreateCompressionOutputPath_WhenTargetExists_AppendsNumber()
    {
        string directory = CreateTempDirectory();
        string isoPath = Path.Combine(directory, "Game.iso");
        string existingCsoPath = Path.Combine(directory, "Game.cso");

        try
        {
            File.WriteAllBytes(isoPath, [1]);
            File.WriteAllBytes(existingCsoPath, [2]);

            CsoOutputPathPolicy policy = new();
            string outputPath = policy.CreateCompressionOutputPath(isoPath);

            Assert.Equal(Path.Combine(directory, "Game-2.cso"), outputPath);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void CreateCompressionOutputPath_WhenNumberedTargetExists_UsesNextNumber()
    {
        string directory = CreateTempDirectory();
        string isoPath = Path.Combine(directory, "Game.iso");

        try
        {
            File.WriteAllBytes(isoPath, [1]);
            File.WriteAllBytes(Path.Combine(directory, "Game.cso"), [2]);
            File.WriteAllBytes(Path.Combine(directory, "Game-2.cso"), [3]);

            CsoOutputPathPolicy policy = new();
            string outputPath = policy.CreateCompressionOutputPath(isoPath);

            Assert.Equal(Path.Combine(directory, "Game-3.cso"), outputPath);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void CreateDecompressionOutputPath_WhenTargetExists_AppendsNumber()
    {
        string directory = CreateTempDirectory();
        string csoPath = Path.Combine(directory, "Game.cso");

        try
        {
            File.WriteAllBytes(csoPath, [1]);
            File.WriteAllBytes(Path.Combine(directory, "Game.iso"), [2]);

            CsoOutputPathPolicy policy = new();
            string outputPath = policy.CreateDecompressionOutputPath(csoPath);

            Assert.Equal(Path.Combine(directory, "Game-2.iso"), outputPath);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }


    [Fact]
    public void CreateCompressionOutputPath_WithLongInputName_TruncatesBaseNameToTenCharacters()
    {
        string directory = CreateTempDirectory();
        string isoPath = Path.Combine(directory, "VeryLongGameName.iso");

        try
        {
            File.WriteAllBytes(isoPath, [1]);

            string outputPath = new CsoOutputPathPolicy().CreateCompressionOutputPath(isoPath);

            Assert.Equal(Path.Combine(directory, "VeryLongGa.cso"), outputPath);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void CreateCompressionOutputPath_WithOneCharacterInputName_PadsBaseNameToTwoCharacters()
    {
        string directory = CreateTempDirectory();
        string isoPath = Path.Combine(directory, "A.iso");

        try
        {
            File.WriteAllBytes(isoPath, [1]);

            string outputPath = new CsoOutputPathPolicy().CreateCompressionOutputPath(isoPath);

            Assert.Equal(Path.Combine(directory, "A_.cso"), outputPath);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"CsoKit_{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
