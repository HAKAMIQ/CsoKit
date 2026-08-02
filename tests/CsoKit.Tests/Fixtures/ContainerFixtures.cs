using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using CsoKit.Core.Formats.Cso;
using CsoKit.Tests.Formats.Cso;

namespace CsoKit.Tests.Fixtures;

internal static class ContainerFixtures
{
    public const int IsoSectorSize = 2048;

    public static byte[] DeterministicBytes(int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        byte[] bytes = new byte[length];

        for (int index = 0; index < bytes.Length; index++)
        {
            bytes[index] = (byte)((index / 64) % 8 == 0 ? 0x41 : (index % 17));
        }

        return bytes;
    }

    public static string CreateCso1Compressed(byte[] logicalBytes)
    {
        return CsoTestFileFactory.CreateTempCsoV1(logicalBytes);
    }

    public static string CreateCso1Stored(byte[] logicalBytes)
    {
        return CsoTestFileFactory.CreateTempCsoV1(logicalBytes, storeBlockUncompressed: static _ => true);
    }

    public static string CreateCso1Mixed(byte[] logicalBytes)
    {
        return CsoTestFileFactory.CreateTempCsoV1(logicalBytes, storeBlockUncompressed: static block => block % 2 == 1);
    }

    public static string CreateCso2Lz4(byte[] logicalBytes)
    {
        return CsoTestFileFactory.CreateTempCso2(logicalBytes);
    }

    public static string CreateCso2Deflate(byte[] logicalBytes)
    {
        string path = CsoTestFileFactory.CreateTempCsoV1(logicalBytes);
        byte[] bytes = File.ReadAllBytes(path);
        bytes[20] = 2;
        File.WriteAllBytes(path, bytes);
        return path;
    }

    public static string CreateZso(byte[] logicalBytes)
    {
        return CsoTestFileFactory.CreateTempZso(logicalBytes);
    }

    public static string CreateDaxCompressed(byte[] logicalBytes)
    {
        return CsoTestFileFactory.CreateTempDax(logicalBytes);
    }

    public static string CreateDaxWithNonCompressedArea(byte[] logicalBytes)
    {
        if (logicalBytes.Length == 0)
        {
            throw new ArgumentException("Fixture bytes must not be empty.", nameof(logicalBytes));
        }

        const int headerSize = 32;
        const int blockSize = 8192;
        int blockCount = checked((logicalBytes.Length + blockSize - 1) / blockSize);
        int areaTableSize = sizeof(uint) * 2;
        int dataStart = checked(headerSize + (blockCount * sizeof(uint)) + (blockCount * sizeof(ushort)) + areaTableSize);
        uint[] offsets = new uint[blockCount];
        ushort[] sizes = new ushort[blockCount];
        List<byte[]> frames = [];
        int cursor = dataStart;

        for (int blockIndex = 0; blockIndex < blockCount; blockIndex++)
        {
            int sourceOffset = checked(blockIndex * blockSize);
            int sourceLength = Math.Min(blockSize, logicalBytes.Length - sourceOffset);
            byte[] block = logicalBytes[sourceOffset..(sourceOffset + sourceLength)];
            byte[] frame = blockIndex == 0 ? block : CompressZLib(block);

            offsets[blockIndex] = checked((uint)cursor);
            sizes[blockIndex] = checked((ushort)(blockIndex == 0 ? 0 : frame.Length));
            frames.Add(frame);
            cursor += frame.Length;
        }

        byte[] bytes = new byte[cursor];
        bytes[0] = (byte)'D';
        bytes[1] = (byte)'A';
        bytes[2] = (byte)'X';
        bytes[3] = 0;
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4, 4), checked((uint)logicalBytes.Length));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8, 4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12, 4), 1);

        int tableCursor = headerSize;

        foreach (uint offset in offsets)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(tableCursor, 4), offset);
            tableCursor += sizeof(uint);
        }

        foreach (ushort size in sizes)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(tableCursor, 2), size);
            tableCursor += sizeof(ushort);
        }

        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(tableCursor, 4), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(tableCursor + 4, 4), 1);
        tableCursor += areaTableSize;

        foreach (byte[] frame in frames)
        {
            frame.CopyTo(bytes, tableCursor);
            tableCursor += frame.Length;
        }

        string path = TempPath(".dax");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    public static string CreateRandomInput()
    {
        string path = TempPath(".bin");
        File.WriteAllBytes(path, DeterministicBytes(128));
        return path;
    }

    public static string CreateMinimalIso9660()
    {
        string path = TempPath(".iso");
        File.WriteAllBytes(path, CreateMinimalPspIso("ULUS-12345", "ULUS-12345", "CsoKit Fixture", includeParamSfo: true));
        return path;
    }

    public static string CreateMinimalPspIsoPath(
        string umdDiscId = "ULUS-12345",
        string paramDiscId = "ULUS-12345",
        bool includeParamSfo = true,
        bool corruptParamSfo = false)
    {
        string path = TempPath(".iso");
        File.WriteAllBytes(path, CreateMinimalPspIso(umdDiscId, paramDiscId, "CsoKit Fixture", includeParamSfo, corruptParamSfo));
        return path;
    }

    public static byte[] CreateMinimalPspIso(
        string umdDiscId,
        string paramDiscId,
        string title,
        bool includeParamSfo,
        bool corruptParamSfo = false)
    {
        byte[] iso = new byte[32 * IsoSectorSize];
        WritePrimaryVolumeDescriptor(iso);
        WriteRootDirectory(iso);
        WritePspGameDirectory(iso, includeParamSfo);
        WriteSysdirDirectory(iso);

        byte[] umdData = Encoding.ASCII.GetBytes($"{umdDiscId}|0001\0");
        umdData.CopyTo(iso.AsSpan(23 * IsoSectorSize));

        if (includeParamSfo)
        {
            byte[] paramSfo = corruptParamSfo
                ? [0x42, 0x41, 0x44, 0x00]
                : CreateParamSfo(paramDiscId, title);
            paramSfo.CopyTo(iso.AsSpan(24 * IsoSectorSize));
        }

        iso[25 * IsoSectorSize] = 0x7F;
        return iso;
    }

    public static string CreateTruncatedCopy(string inputPath, int bytesToRemove = 8)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bytesToRemove);

        byte[] bytes = File.ReadAllBytes(inputPath);
        Array.Resize(ref bytes, Math.Max(0, bytes.Length - bytesToRemove));
        string path = TempPath(Path.GetExtension(inputPath));
        File.WriteAllBytes(path, bytes);
        return path;
    }

    public static string TempPath(string extension)
    {
        string fileName = Guid.NewGuid().ToString("N", System.Globalization.CultureInfo.InvariantCulture)[..8];
        return Path.Combine(Path.GetTempPath(), fileName + Path.GetExtension(extension));
    }

    private static byte[] CreateParamSfo(string discId, string title)
    {
        (string Key, string Value)[] values =
        [
            ("TITLE", title),
            ("DISC_ID", discId),
            ("CATEGORY", "UG"),
            ("PSP_SYSTEM_VER", "6.60"),
        ];

        byte[] keyTable = CreateParamSfoKeyTable(values);
        byte[][] dataValues = new byte[values.Length][];
        int dataValuesSize = 0;

        for (int index = 0; index < values.Length; index++)
        {
            byte[] data = Encoding.UTF8.GetBytes(values[index].Value + "\0");
            dataValues[index] = data;
            dataValuesSize += data.Length;
        }

        int headerSize = 20;
        int entryTableSize = values.Length * 16;
        int keyTableOffset = headerSize + entryTableSize;
        int dataTableOffset = keyTableOffset + keyTable.Length;
        int totalSize = dataTableOffset + dataValuesSize;
        byte[] bytes = new byte[totalSize];

        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0, 4), 0x46535000);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4, 4), 0x00000101);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8, 4), checked((uint)keyTableOffset));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12, 4), checked((uint)dataTableOffset));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16, 4), checked((uint)values.Length));

        int keyOffset = 0;
        int dataOffset = 0;

        for (int index = 0; index < values.Length; index++)
        {
            int entryOffset = headerSize + (index * 16);
            byte[] data = dataValues[index];
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(entryOffset, 2), checked((ushort)keyOffset));
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(entryOffset + 2, 2), 0x0204);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(entryOffset + 4, 4), checked((uint)data.Length));
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(entryOffset + 8, 4), checked((uint)data.Length));
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(entryOffset + 12, 4), checked((uint)dataOffset));

            keyOffset += values[index].Key.Length + 1;
            data.CopyTo(bytes, dataTableOffset + dataOffset);
            dataOffset += data.Length;
        }

        keyTable.CopyTo(bytes, keyTableOffset);
        return bytes;
    }

    private static byte[] CreateParamSfoKeyTable((string Key, string Value)[] values)
    {
        using MemoryStream stream = new();

        foreach ((string key, _) in values)
        {
            byte[] keyBytes = Encoding.ASCII.GetBytes(key);
            stream.Write(keyBytes);
            stream.WriteByte(0);
        }

        return stream.ToArray();
    }

    private static void WritePrimaryVolumeDescriptor(byte[] iso)
    {
        int pvdOffset = 16 * IsoSectorSize;
        iso[pvdOffset] = 1;
        "CD001"u8.CopyTo(iso.AsSpan(pvdOffset + 1));
        iso[pvdOffset + 6] = 1;
        WriteDirectoryRecord(iso.AsSpan(pvdOffset + 156), "\0", 20, IsoSectorSize, isDirectory: true);
    }

    private static void WriteRootDirectory(byte[] iso)
    {
        Span<byte> directory = iso.AsSpan(20 * IsoSectorSize, IsoSectorSize);
        int offset = 0;
        WriteDirectoryRecord(directory[offset..], "\0", 20, IsoSectorSize, isDirectory: true);
        offset += directory[offset];
        WriteDirectoryRecord(directory[offset..], "\u0001", 20, IsoSectorSize, isDirectory: true);
        offset += directory[offset];
        WriteDirectoryRecord(directory[offset..], "UMD_DATA.BIN;1", 23, 32, isDirectory: false);
        offset += directory[offset];
        WriteDirectoryRecord(directory[offset..], "PSP_GAME", 21, IsoSectorSize, isDirectory: true);
    }

    private static void WritePspGameDirectory(byte[] iso, bool includeParamSfo)
    {
        Span<byte> directory = iso.AsSpan(21 * IsoSectorSize, IsoSectorSize);
        int offset = 0;
        WriteDirectoryRecord(directory[offset..], "\0", 21, IsoSectorSize, isDirectory: true);
        offset += directory[offset];
        WriteDirectoryRecord(directory[offset..], "\u0001", 20, IsoSectorSize, isDirectory: true);
        offset += directory[offset];

        if (includeParamSfo)
        {
            WriteDirectoryRecord(directory[offset..], "PARAM.SFO;1", 24, 4096, isDirectory: false);
            offset += directory[offset];
        }

        WriteDirectoryRecord(directory[offset..], "SYSDIR", 22, IsoSectorSize, isDirectory: true);
    }

    private static void WriteSysdirDirectory(byte[] iso)
    {
        Span<byte> directory = iso.AsSpan(22 * IsoSectorSize, IsoSectorSize);
        int offset = 0;
        WriteDirectoryRecord(directory[offset..], "\0", 22, IsoSectorSize, isDirectory: true);
        offset += directory[offset];
        WriteDirectoryRecord(directory[offset..], "\u0001", 21, IsoSectorSize, isDirectory: true);
        offset += directory[offset];
        WriteDirectoryRecord(directory[offset..], "EBOOT.BIN;1", 25, 1, isDirectory: false);
    }

    private static void WriteDirectoryRecord(
        Span<byte> destination,
        string name,
        uint extent,
        uint size,
        bool isDirectory)
    {
        byte[] nameBytes = Encoding.ASCII.GetBytes(name);
        int length = 33 + nameBytes.Length;

        if ((length & 1) != 0)
        {
            length++;
        }

        destination[0] = checked((byte)length);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[2..6], extent);
        BinaryPrimitives.WriteUInt32BigEndian(destination[6..10], extent);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[10..14], size);
        BinaryPrimitives.WriteUInt32BigEndian(destination[14..18], size);
        destination[25] = isDirectory ? (byte)0x02 : (byte)0x00;
        destination[28] = 1;
        destination[32] = checked((byte)nameBytes.Length);
        nameBytes.CopyTo(destination[33..]);
    }

    private static byte[] CompressZLib(byte[] block)
    {
        using MemoryStream compressed = new();

        using (ZLibStream zlib = new(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            zlib.Write(block);
        }

        return compressed.ToArray();
    }
}