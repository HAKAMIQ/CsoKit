using System.Buffers.Binary;
using System.Text;
using CsoKit.Core.Formats.Cso;

namespace CsoKit.Core.Formats.DiscImage;

public static class FormatDetector
{
    public const int ProbeBytes = 64 * 1024;
    private const int Iso9660PrimaryVolumeDescriptorOffset = 16 * 2048;

    public static FormatDetectionResult Detect(string inputPath)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            return FormatDetectionResult.Fail("InvalidInputPath", "Input path is empty.");
        }

        if (!File.Exists(inputPath))
        {
            return FormatDetectionResult.Fail("InputNotFound", "Input file was not found.");
        }

        try
        {
            using FileStream input = new(
                inputPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: ProbeBytes,
                FileOptions.SequentialScan);

            return Detect(input);
        }
        catch (UnauthorizedAccessException ex)
        {
            return FormatDetectionResult.Fail("InputAccessDenied", ex.Message);
        }
        catch (IOException ex)
        {
            return FormatDetectionResult.Fail("InputReadFailed", ex.Message);
        }
    }

    public static FormatDetectionResult Detect(Stream input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (!input.CanRead)
        {
            return FormatDetectionResult.Fail("StreamNotReadable", "Input stream is not readable.");
        }

        byte[] probe = new byte[ProbeBytes];
        int read = CsoBlockReader.ReadExactlyOrLess(input, probe);
        ReadOnlySpan<byte> data = probe.AsSpan(0, read);
        List<string> warnings = [];

        if (data.Length == 0)
        {
            return FormatDetectionResult.Fail("EmptyInput", "Input file is empty.");
        }

        string magic = ReadMagic(data);

        if (data.Length >= CsoConstants.MinimumHeaderSize &&
            data[..4].SequenceEqual(CsoConstants.MagicBytes))
        {
            uint headerSize = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(4, 4));
            ulong uncompressedSize = BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(8, 8));
            uint blockSize = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(16, 4));
            byte version = data[20];
            byte indexShift = data[21];
            DetectedDiscFormat format = version switch
            {
                0 or 1 => DetectedDiscFormat.Cso1,
                2 => DetectedDiscFormat.Cso2,
                _ => DetectedDiscFormat.Unknown,
            };

            if (blockSize == 0)
            {
                warnings.Add("CSO block size is zero.");
            }

            if (blockSize != CsoCompressor.DefaultBlockSize)
            {
                warnings.Add("CSO block size is not the game-safe default 2048 bytes.");
            }

            if (version > 2)
            {
                warnings.Add("CSO version is not supported by the current reader.");
            }

            long? sectorCount = blockSize == 0
                ? null
                : checked((long)((uncompressedSize + blockSize - 1) / blockSize));

            return new FormatDetectionResult(
                true,
                format,
                "CISO",
                headerSize,
                uncompressedSize,
                blockSize,
                indexShift,
                sectorCount,
                warnings);
        }

        if (data.Length >= 4 &&
            data[..4].SequenceEqual("ZISO"u8))
        {
            warnings.Add("ZSO input is detected for intake/normalization only; CSO1 output remains the safe default.");

            if (data.Length >= CsoConstants.MinimumHeaderSize)
            {
                uint headerSize = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(4, 4));
                ulong uncompressedSize = BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(8, 8));
                uint blockSize = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(16, 4));
                byte indexShift = data[21];
                long? sectorCount = blockSize == 0
                    ? null
                    : checked((long)((uncompressedSize + blockSize - 1) / blockSize));

                return new FormatDetectionResult(
                    true,
                    DetectedDiscFormat.Zso,
                    "ZISO",
                    headerSize,
                    uncompressedSize,
                    blockSize,
                    indexShift,
                    sectorCount,
                    warnings);
            }

            return new FormatDetectionResult(
                true,
                DetectedDiscFormat.Zso,
                "ZISO",
                null,
                null,
                null,
                null,
                null,
                warnings);
        }

        if (data.Length >= 4 &&
            (data[..4].SequenceEqual("DAX\0"u8) || data[..3].SequenceEqual("DAX"u8)))
        {
            warnings.Add("DAX input is detected for intake/normalization only; CSO1 output remains the safe default.");

            if (data.Length >= 32)
            {
                uint uncompressedSize = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(4, 4));
                long sectorCount = checked((uncompressedSize + 8191L) / 8192L);

                return new FormatDetectionResult(
                    true,
                    DetectedDiscFormat.Dax,
                    "DAX",
                    32,
                    uncompressedSize,
                    8192,
                    null,
                    sectorCount,
                    warnings);
            }

            return new FormatDetectionResult(
                true,
                DetectedDiscFormat.Dax,
                "DAX",
                null,
                null,
                null,
                null,
                null,
                warnings);
        }

        if (LooksLikeIso9660(data))
        {
            return new FormatDetectionResult(
                true,
                DetectedDiscFormat.RawIso,
                "ISO9660",
                null,
                null,
                2048,
                null,
                input.CanSeek ? checked((input.Length + 2047) / 2048) : null,
                warnings);
        }

        warnings.Add("Unknown format. No CSO/ZSO/DAX magic or ISO9660 primary volume descriptor was found in the first 64KB.");

        return new FormatDetectionResult(
            true,
            DetectedDiscFormat.Unknown,
            magic,
            null,
            null,
            null,
            null,
            null,
            warnings);
    }

    private static bool LooksLikeIso9660(ReadOnlySpan<byte> data)
    {
        return data.Length >= Iso9660PrimaryVolumeDescriptorOffset + 6 &&
            data[Iso9660PrimaryVolumeDescriptorOffset] == 1 &&
            data.Slice(Iso9660PrimaryVolumeDescriptorOffset + 1, 5).SequenceEqual("CD001"u8);
    }

    private static string ReadMagic(ReadOnlySpan<byte> data)
    {
        int length = Math.Min(8, data.Length);

        if (length == 0)
        {
            return string.Empty;
        }

        StringBuilder builder = new(length * 2);

        for (int i = 0; i < length; i++)
        {
            byte value = data[i];

            if (value >= 0x20 && value <= 0x7E)
            {
                builder.Append((char)value);
            }
            else
            {
                builder.Append("\\x");
                builder.Append(value.ToString("X2"));
            }
        }

        return builder.ToString();
    }
}