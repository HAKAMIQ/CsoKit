using System.Buffers.Binary;

namespace CsoKit.App.Tests;

internal sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"csokit-app-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string CreateFile(string fileName, byte[]? contents = null)
    {
        string path = System.IO.Path.Combine(Path, fileName);
        File.WriteAllBytes(path, contents ?? [0]);
        return path;
    }

    public string Combine(string fileName)
    {
        return System.IO.Path.Combine(Path, fileName);
    }

    /// <summary>Writes a minimal CSO1 header that the format detector accepts.</summary>
    public string CreateCsoHeaderFile(string fileName, ulong uncompressedSize = 4096)
    {
        byte[] header = new byte[24];
        "CISO"u8.CopyTo(header);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4, 4), 24);
        BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(8, 8), uncompressedSize);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(16, 4), 2048);
        header[20] = 1;
        return CreateFile(fileName, header);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
