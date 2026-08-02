using System.Buffers.Binary;

namespace CsoKit.Core.Formats.Cso;

public sealed class CsoHeaderReader
{
    public CsoHeaderReadResult Read(string inputPath)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            return CsoHeaderReadResult.Fail("InvalidInputPath", "Input path is empty.");
        }

        if (!File.Exists(inputPath))
        {
            return CsoHeaderReadResult.Fail("InputNotFound", "Input file was not found.");
        }

        try
        {
            using FileStream stream = new(
                inputPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: CsoConstants.MinimumHeaderSize,
                FileOptions.SequentialScan);

            return Read(stream);
        }
        catch (UnauthorizedAccessException ex)
        {
            return CsoHeaderReadResult.Fail("InputAccessDenied", ex.Message);
        }
        catch (IOException ex)
        {
            return CsoHeaderReadResult.Fail("InputReadFailed", ex.Message);
        }
    }

    public CsoHeaderReadResult Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        Span<byte> headerBytes = stackalloc byte[CsoConstants.MinimumHeaderSize];
        int read = ReadExactlyOrLess(stream, headerBytes);

        if (read < CsoConstants.MinimumHeaderSize)
        {
            return CsoHeaderReadResult.Fail(
                "HeaderTooSmall",
                $"CSO header is too small. Expected at least {CsoConstants.MinimumHeaderSize} bytes.");
        }

        if (!headerBytes[..4].SequenceEqual(CsoConstants.MagicBytes))
        {
            return CsoHeaderReadResult.Fail(
                "InvalidMagic",
                "Invalid CSO magic. Expected CISO.");
        }

        uint headerSize = BinaryPrimitives.ReadUInt32LittleEndian(headerBytes.Slice(4, 4));
        ulong uncompressedSize = BinaryPrimitives.ReadUInt64LittleEndian(headerBytes.Slice(8, 8));
        uint blockSize = BinaryPrimitives.ReadUInt32LittleEndian(headerBytes.Slice(16, 4));
        byte version = headerBytes[20];
        byte indexShift = headerBytes[21];

        if (version is not (0 or 1 or 2))
        {
            return CsoHeaderReadResult.Fail(
                "UnsupportedVersion",
                "Unsupported CSO file.");
        }

        if (version == 2 && headerSize < CsoConstants.MinimumHeaderSize)
        {
            return CsoHeaderReadResult.Fail(
                "InvalidHeaderSize",
                $"Invalid CSO header size: {headerSize}.");
        }

        if (uncompressedSize == 0)
        {
            return CsoHeaderReadResult.Fail(
                "InvalidUncompressedSize",
                "CSO uncompressed size is zero.");
        }

        if (blockSize == 0)
        {
            return CsoHeaderReadResult.Fail(
                "InvalidBlockSize",
                "CSO block size is zero.");
        }

        if (blockSize > CsoConstants.MaxSupportedBlockSize)
        {
            return CsoHeaderReadResult.Fail(
                "BlockSizeTooLarge",
                $"CSO block size is too large. Maximum supported block size is {CsoConstants.MaxSupportedBlockSize:N0} bytes.");
        }

        if (indexShift > CsoConstants.MaxSupportedIndexShift)
        {
            return CsoHeaderReadResult.Fail(
                "InvalidIndexShift",
                $"CSO index shift is too large. Maximum supported shift is {CsoConstants.MaxSupportedIndexShift}.");
        }

        CsoHeader header = new(
            headerSize,
            uncompressedSize,
            blockSize,
            version,
            indexShift);

        return CsoHeaderReadResult.Ok(header);
    }

    private static int ReadExactlyOrLess(Stream stream, Span<byte> buffer)
    {
        int totalRead = 0;

        while (totalRead < buffer.Length)
        {
            int read = stream.Read(buffer[totalRead..]);

            if (read == 0)
            {
                break;
            }

            totalRead += read;
        }

        return totalRead;
    }
}
