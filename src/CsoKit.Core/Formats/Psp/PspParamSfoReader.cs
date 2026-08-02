using System.Buffers.Binary;
using System.Text;

namespace CsoKit.Core.Formats.Psp;

public static class PspParamSfoReader
{
    private const int HeaderSize = 20;
    private const int EntrySize = 16;
    private const uint Magic = 0x46535000;

    public static bool TryRead(ReadOnlySpan<byte> bytes, out PspDiscIdentity identity, out string? warning)
    {
        identity = PspDiscIdentity.Empty;
        warning = null;

        try
        {
            if (bytes.Length < HeaderSize)
            {
                warning = "PARAM.SFO is too small to contain a valid header.";
                return false;
            }

            uint magic = BinaryPrimitives.ReadUInt32LittleEndian(bytes[..4]);

            if (magic != Magic)
            {
                warning = "PARAM.SFO magic did not match the expected PSP SFO signature.";
                return false;
            }

            uint keyTableOffset = BinaryPrimitives.ReadUInt32LittleEndian(bytes[8..12]);
            uint dataTableOffset = BinaryPrimitives.ReadUInt32LittleEndian(bytes[12..16]);
            uint entryCount = BinaryPrimitives.ReadUInt32LittleEndian(bytes[16..20]);

            if (entryCount > 1024)
            {
                warning = "PARAM.SFO entry table is unreasonably large.";
                return false;
            }

            ulong tableEnd = checked((ulong)HeaderSize + ((ulong)entryCount * EntrySize));

            if (tableEnd > (ulong)bytes.Length ||
                keyTableOffset >= (uint)bytes.Length ||
                dataTableOffset >= (uint)bytes.Length)
            {
                warning = "PARAM.SFO table offsets are outside the file.";
                return false;
            }

            int entryCountValue = checked((int)entryCount);
            Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);

            for (int index = 0; index < entryCountValue; index++)
            {
                int entryOffset = checked(HeaderSize + (index * EntrySize));
                ReadOnlySpan<byte> entry = bytes[entryOffset..(entryOffset + EntrySize)];
                ushort keyOffset = BinaryPrimitives.ReadUInt16LittleEndian(entry[..2]);
                uint dataLength = BinaryPrimitives.ReadUInt32LittleEndian(entry[4..8]);
                uint dataOffset = BinaryPrimitives.ReadUInt32LittleEndian(entry[12..16]);

                if (dataLength == 0)
                {
                    continue;
                }

                ulong absoluteKeyOffset = checked((ulong)keyTableOffset + keyOffset);
                ulong absoluteDataOffset = checked((ulong)dataTableOffset + dataOffset);
                ulong absoluteDataEnd = checked(absoluteDataOffset + dataLength);

                if (absoluteKeyOffset >= (ulong)bytes.Length || absoluteDataEnd > (ulong)bytes.Length)
                {
                    continue;
                }

                string key = ReadNullTerminatedAscii(bytes[(int)absoluteKeyOffset..]);

                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                if (!IsWantedKey(key))
                {
                    continue;
                }

                ReadOnlySpan<byte> valueBytes = bytes[(int)absoluteDataOffset..(int)absoluteDataEnd];
                string value = ReadNullTerminatedUtf8(valueBytes);

                if (!string.IsNullOrWhiteSpace(value))
                {
                    values[key] = value;
                }
            }

            values.TryGetValue("TITLE", out string? title);
            values.TryGetValue("DISC_ID", out string? discId);
            values.TryGetValue("CATEGORY", out string? category);
            values.TryGetValue("PSP_SYSTEM_VER", out string? pspSystemVersion);

            identity = new PspDiscIdentity(
                Clean(title),
                Clean(discId),
                Clean(category),
                Clean(pspSystemVersion));

            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or ArithmeticException or DecoderFallbackException)
        {
            warning = "PARAM.SFO could not be parsed safely.";
            identity = PspDiscIdentity.Empty;
            return false;
        }
    }

    private static bool IsWantedKey(string key)
    {
        return string.Equals(key, "TITLE", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(key, "DISC_ID", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(key, "CATEGORY", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(key, "PSP_SYSTEM_VER", StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadNullTerminatedAscii(ReadOnlySpan<byte> bytes)
    {
        int zero = bytes.IndexOf((byte)0);
        ReadOnlySpan<byte> text = zero >= 0 ? bytes[..zero] : bytes;
        return Encoding.ASCII.GetString(text);
    }

    private static string ReadNullTerminatedUtf8(ReadOnlySpan<byte> bytes)
    {
        int zero = bytes.IndexOf((byte)0);
        ReadOnlySpan<byte> text = zero >= 0 ? bytes[..zero] : bytes;
        return Encoding.UTF8.GetString(text);
    }

    private static string? Clean(string? value)
    {
        string? cleaned = value?.Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? null : cleaned;
    }
}