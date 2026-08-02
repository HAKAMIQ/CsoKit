using System.Buffers.Binary;

namespace CsoKit.Core.Formats.Iso;

public sealed class Iso9660Reader
{
    private const int PrimaryVolumeDescriptorSector = 16;
    private const int RootDirectoryRecordOffset = (PrimaryVolumeDescriptorSector * IsoAlignmentPolicy.SectorSize) + 156;
    private static readonly char[] PathSeparators = ['/', '\\'];

    private readonly Stream input;

    public Iso9660Reader(Stream input)
    {
        this.input = input ?? throw new ArgumentNullException(nameof(input));

        if (!input.CanRead)
        {
            throw new ArgumentException("Input stream must be readable.", nameof(input));
        }

        if (!input.CanSeek)
        {
            throw new ArgumentException("Input stream must be seekable.", nameof(input));
        }
    }

    public bool HasPrimaryVolumeDescriptor()
    {
        if (input.Length < ((PrimaryVolumeDescriptorSector + 1L) * IsoAlignmentPolicy.SectorSize))
        {
            return false;
        }

        Span<byte> descriptor = stackalloc byte[6];
        input.Position = PrimaryVolumeDescriptorSector * IsoAlignmentPolicy.SectorSize;
        ReadExactly(input, descriptor);

        return descriptor[0] == 1 &&
            descriptor[1..6].SequenceEqual("CD001"u8);
    }

    public bool TryFindPath(string path, out Iso9660Entry entry)
    {
        entry = default!;

        if (string.IsNullOrWhiteSpace(path) || !HasPrimaryVolumeDescriptor())
        {
            return false;
        }

        Iso9660Entry root = ReadRootDirectory();
        string[] parts = path.Split(
            PathSeparators,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        Iso9660Entry current = root;

        foreach (string rawPart in parts)
        {
            if (!current.IsDirectory)
            {
                return false;
            }

            string part = NormalizeName(rawPart);
            IReadOnlyList<Iso9660Entry> entries = ReadDirectory(current);
            Iso9660Entry? next = null;

            for (int index = 0; index < entries.Count; index++)
            {
                Iso9660Entry candidate = entries[index];

                if (string.Equals(NormalizeName(candidate.Name), part, StringComparison.Ordinal))
                {
                    next = candidate;
                    break;
                }
            }

            if (next is null)
            {
                return false;
            }

            current = next;
        }

        entry = current;
        return true;
    }

    public byte[] ReadFile(Iso9660Entry file, int maxBytes)
    {
        if (file.IsDirectory)
        {
            throw new InvalidDataException("ISO9660 entry is a directory, not a file.");
        }

        if (maxBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxBytes), "Maximum read size must be positive.");
        }

        if (file.Size > maxBytes)
        {
            throw new InvalidDataException($"ISO9660 file {file.Name} is larger than the safe read limit of {maxBytes:N0} bytes.");
        }

        ulong start = (ulong)file.Extent * IsoAlignmentPolicy.SectorSize;
        ulong end = checked(start + file.Size);

        if (end > (ulong)input.Length)
        {
            throw new EndOfStreamException($"ISO9660 file {file.Name} extends beyond the end of the file.");
        }

        byte[] buffer = new byte[checked((int)file.Size)];
        input.Position = checked((long)start);
        ReadExactly(input, buffer);
        return buffer;
    }

    public IReadOnlyList<Iso9660Entry> ReadDirectory(Iso9660Entry directory)
    {
        if (!directory.IsDirectory)
        {
            throw new InvalidDataException("ISO9660 entry is not a directory.");
        }

        if (directory.Size > int.MaxValue)
        {
            throw new InvalidDataException("ISO9660 directory is too large.");
        }

        ulong start = (ulong)directory.Extent * IsoAlignmentPolicy.SectorSize;
        ulong end = checked(start + directory.Size);

        if (end > (ulong)input.Length)
        {
            throw new EndOfStreamException("ISO9660 directory extends beyond the end of the file.");
        }

        byte[] buffer = new byte[checked((int)directory.Size)];
        input.Position = checked((long)start);
        ReadExactly(input, buffer);

        List<Iso9660Entry> entries = [];
        int offset = 0;

        while (offset < buffer.Length)
        {
            int length = buffer[offset];

            if (length == 0)
            {
                offset = AlignToNextSector(offset);
                continue;
            }

            if (offset + length > buffer.Length)
            {
                throw new InvalidDataException("ISO9660 directory record is truncated.");
            }

            ReadOnlySpan<byte> record = buffer.AsSpan(offset, length);
            Iso9660Entry? entry = TryReadDirectoryRecord(record);

            if (entry is not null)
            {
                entries.Add(entry);
            }

            offset += length;
        }

        return entries;
    }

    private Iso9660Entry ReadRootDirectory()
    {
        Span<byte> rootRecordPrefix = stackalloc byte[1];
        input.Position = RootDirectoryRecordOffset;
        ReadExactly(input, rootRecordPrefix);

        int length = rootRecordPrefix[0];

        if (length < 34)
        {
            throw new InvalidDataException("ISO9660 root directory record is invalid.");
        }

        byte[] record = new byte[length];
        record[0] = rootRecordPrefix[0];
        ReadExactly(input, record.AsSpan(1));

        return TryReadDirectoryRecord(record, includeSpecialDirectoryNames: true) ??
            throw new InvalidDataException("ISO9660 root directory record could not be read.");
    }

    private static Iso9660Entry? TryReadDirectoryRecord(
        ReadOnlySpan<byte> record,
        bool includeSpecialDirectoryNames = false)
    {
        if (record.Length < 34)
        {
            return null;
        }

        uint extent = BinaryPrimitives.ReadUInt32LittleEndian(record.Slice(2, 4));
        uint size = BinaryPrimitives.ReadUInt32LittleEndian(record.Slice(10, 4));
        byte flags = record[25];
        byte nameLength = record[32];

        if (33 + nameLength > record.Length)
        {
            return null;
        }

        ReadOnlySpan<byte> rawName = record.Slice(33, nameLength);

        if (rawName.Length == 1 && (rawName[0] == 0 || rawName[0] == 1))
        {
            if (!includeSpecialDirectoryNames)
            {
                return null;
            }

            string specialName = rawName[0] == 0 ? "." : "..";

            return new Iso9660Entry(
                specialName,
                extent,
                size,
                IsDirectory: (flags & 0x02) != 0);
        }

        string name = System.Text.Encoding.ASCII.GetString(rawName);
        int versionMarker = name.IndexOf(';', StringComparison.Ordinal);

        if (versionMarker >= 0)
        {
            name = name[..versionMarker];
        }

        return new Iso9660Entry(
            name,
            extent,
            size,
            IsDirectory: (flags & 0x02) != 0);
    }

    private static string NormalizeName(string name)
    {
        return name.Trim().TrimEnd('.').ToUpperInvariant();
    }

    private static int AlignToNextSector(int offset)
    {
        int remainder = offset % IsoAlignmentPolicy.SectorSize;
        return remainder == 0
            ? offset + IsoAlignmentPolicy.SectorSize
            : offset + (IsoAlignmentPolicy.SectorSize - remainder);
    }

    private static void ReadExactly(Stream stream, Span<byte> buffer)
    {
        int totalRead = 0;

        while (totalRead < buffer.Length)
        {
            int read = stream.Read(buffer[totalRead..]);

            if (read == 0)
            {
                throw new EndOfStreamException("Unexpected end of ISO9660 stream.");
            }

            totalRead += read;
        }
    }
}