using System.Buffers.Binary;
using CsoKit.Core.Formats.Iso;

namespace CsoKit.Tests.Formats.Iso;

public sealed class PspIsoValidatorTests
{
    private const int SectorSize = 2048;

    [Fact]
    public void Validate_WithMinimalPspIso_ReturnsSuccess()
    {
        string isoPath = CreateTempPath(".iso");

        try
        {
            File.WriteAllBytes(isoPath, CreateMinimalPspIso(includeRequiredPaths: true));

            PspIsoValidationResult result = PspIsoValidator.Validate(isoPath);

            Assert.True(result.Success);
            Assert.True(result.HasIso9660PrimaryVolumeDescriptor);
            Assert.True(result.HasUmdDataBin);
            Assert.True(result.HasParamSfo);
            Assert.True(result.HasEbootBin);
        }
        finally
        {
            File.Delete(isoPath);
        }
    }

    [Fact]
    public void Validate_WithMissingPspPaths_ReturnsStructuredErrors()
    {
        string isoPath = CreateTempPath(".iso");

        try
        {
            File.WriteAllBytes(isoPath, CreateMinimalPspIso(includeRequiredPaths: false));

            PspIsoValidationResult result = PspIsoValidator.Validate(isoPath);

            Assert.False(result.Success);
            Assert.Contains(result.Issues, issue => issue.Code == "MissingUmdDataBin");
            Assert.Contains(result.Issues, issue => issue.Code == "MissingPspGameDirectory");
            Assert.DoesNotContain(result.Issues, issue => issue.Code == "MissingParamSfo");
            Assert.DoesNotContain(result.Issues, issue => issue.Code == "MissingEbootBin");
        }
        finally
        {
            File.Delete(isoPath);
        }
    }

    [Fact]
    public void Validate_WithUnalignedIso_ReturnsIsoNotSectorAligned()
    {
        string isoPath = CreateTempPath(".iso");

        try
        {
            File.WriteAllBytes(isoPath, [1, 2, 3]);

            PspIsoValidationResult result = PspIsoValidator.Validate(isoPath);

            Assert.False(result.Success);
            Assert.Contains(result.Issues, issue => issue.Code == "IsoNotSectorAligned");
        }
        finally
        {
            File.Delete(isoPath);
        }
    }

    internal static byte[] CreateMinimalPspIso(bool includeRequiredPaths)
    {
        byte[] iso = new byte[32 * SectorSize];

        WritePrimaryVolumeDescriptor(iso);
        WriteRootDirectory(iso, includeRequiredPaths);
        WritePspGameDirectory(iso, includeRequiredPaths);
        WriteSysdirDirectory(iso, includeRequiredPaths);

        return iso;
    }

    private static void WritePrimaryVolumeDescriptor(byte[] iso)
    {
        int pvdOffset = 16 * SectorSize;
        iso[pvdOffset] = 1;
        "CD001"u8.CopyTo(iso.AsSpan(pvdOffset + 1));
        iso[pvdOffset + 6] = 1;

        int rootOffset = pvdOffset + 156;
        WriteDirectoryRecord(
            iso.AsSpan(rootOffset),
            name: "\0",
            extent: 20,
            size: SectorSize,
            isDirectory: true);
    }

    private static void WriteRootDirectory(byte[] iso, bool includeRequiredPaths)
    {
        Span<byte> directory = iso.AsSpan(20 * SectorSize, SectorSize);
        int offset = 0;

        WriteDirectoryRecord(directory[offset..], "\0", 20, SectorSize, isDirectory: true);
        offset += directory[offset];
        WriteDirectoryRecord(directory[offset..], "\u0001", 20, SectorSize, isDirectory: true);
        offset += directory[offset];

        if (!includeRequiredPaths)
        {
            return;
        }

        WriteDirectoryRecord(directory[offset..], "UMD_DATA.BIN;1", 23, 16, isDirectory: false);
        offset += directory[offset];
        WriteDirectoryRecord(directory[offset..], "PSP_GAME", 21, SectorSize, isDirectory: true);
    }

    private static void WritePspGameDirectory(byte[] iso, bool includeRequiredPaths)
    {
        Span<byte> directory = iso.AsSpan(21 * SectorSize, SectorSize);
        int offset = 0;

        WriteDirectoryRecord(directory[offset..], "\0", 21, SectorSize, isDirectory: true);
        offset += directory[offset];
        WriteDirectoryRecord(directory[offset..], "\u0001", 20, SectorSize, isDirectory: true);
        offset += directory[offset];

        if (!includeRequiredPaths)
        {
            return;
        }

        WriteDirectoryRecord(directory[offset..], "PARAM.SFO;1", 24, 16, isDirectory: false);
        offset += directory[offset];
        WriteDirectoryRecord(directory[offset..], "SYSDIR", 22, SectorSize, isDirectory: true);
    }

    private static void WriteSysdirDirectory(byte[] iso, bool includeRequiredPaths)
    {
        Span<byte> directory = iso.AsSpan(22 * SectorSize, SectorSize);
        int offset = 0;

        WriteDirectoryRecord(directory[offset..], "\0", 22, SectorSize, isDirectory: true);
        offset += directory[offset];
        WriteDirectoryRecord(directory[offset..], "\u0001", 21, SectorSize, isDirectory: true);
        offset += directory[offset];

        if (includeRequiredPaths)
        {
            WriteDirectoryRecord(directory[offset..], "EBOOT.BIN;1", 25, 16, isDirectory: false);
        }
    }

    private static void WriteDirectoryRecord(
        Span<byte> destination,
        string name,
        uint extent,
        uint size,
        bool isDirectory)
    {
        byte[] nameBytes = System.Text.Encoding.ASCII.GetBytes(name);
        int length = 33 + nameBytes.Length;

        destination[0] = checked((byte)length);
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(2, 4), extent);
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(10, 4), size);
        destination[25] = isDirectory ? (byte)0x02 : (byte)0x00;
        destination[32] = checked((byte)nameBytes.Length);
        nameBytes.CopyTo(destination[33..]);
    }

    private static string CreateTempPath(string extension)
    {
        return Path.Combine(Path.GetTempPath(), $"CsoKit_{Guid.NewGuid():N}{extension}");
    }
}