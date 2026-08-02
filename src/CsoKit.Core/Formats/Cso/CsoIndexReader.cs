using System.Buffers.Binary;

namespace CsoKit.Core.Formats.Cso;

public sealed class CsoIndexReader
{
    private const long CsoV1IndexStart = 24;

    public CsoIndexReadResult Read(string inputPath, CsoHeader header)
    {
        ArgumentNullException.ThrowIfNull(header);

        if (string.IsNullOrWhiteSpace(inputPath))
        {
            return CsoIndexReadResult.Fail("InvalidInputPath", "Input path is empty.");
        }

        if (!File.Exists(inputPath))
        {
            return CsoIndexReadResult.Fail("InputNotFound", "Input file was not found.");
        }

        try
        {
            using FileStream stream = new(
                inputPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 64 * 1024,
                FileOptions.SequentialScan);

            return Read(stream, header);
        }
        catch (UnauthorizedAccessException ex)
        {
            return CsoIndexReadResult.Fail("InputAccessDenied", ex.Message);
        }
        catch (IOException ex)
        {
            return CsoIndexReadResult.Fail("InputReadFailed", ex.Message);
        }
    }

    public CsoIndexReadResult Read(Stream stream, CsoHeader header)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(header);

        if (!stream.CanRead)
        {
            return CsoIndexReadResult.Fail("StreamNotReadable", "Input stream is not readable.");
        }

        if (!stream.CanSeek)
        {
            return CsoIndexReadResult.Fail("StreamNotSeekable", "Input stream must be seekable.");
        }

        if (header.SectorCount <= 0)
        {
            return CsoIndexReadResult.Fail("InvalidSectorCount", "CSO sector count is invalid.");
        }

        if (header.IndexEntryCount > int.MaxValue)
        {
            return CsoIndexReadResult.Fail("TooManyIndexEntries", "CSO index table is too large.");
        }

        long indexStart = ResolveIndexStart(header);
        long indexSize = header.IndexTableSizeBytes;
        long indexEnd;

        try
        {
            indexEnd = checked(indexStart + indexSize);
        }
        catch (OverflowException)
        {
            return CsoIndexReadResult.Fail(
                "IndexTableTooLarge",
                "CSO index table range overflows the supported stream address space.");
        }

        if (stream.Length < indexEnd)
        {
            return CsoIndexReadResult.Fail(
                "IndexTableTruncated",
                $"CSO index table is truncated. Expected at least {indexEnd:N0} bytes.");
        }

        stream.Position = indexStart;

        int entryCount = checked((int)header.IndexEntryCount);
        CsoIndexEntry[] entries = new CsoIndexEntry[entryCount];

        Span<byte> rawEntry = stackalloc byte[sizeof(uint)];

        for (int i = 0; i < entryCount; i++)
        {
            int read = ReadExactlyOrLess(stream, rawEntry);

            if (read < rawEntry.Length)
            {
                return CsoIndexReadResult.Fail(
                    "IndexEntryTruncated",
                    $"CSO index entry {i:N0} is truncated.");
            }

            uint rawValue = BinaryPrimitives.ReadUInt32LittleEndian(rawEntry);
            entries[i] = CsoIndexEntry.FromRaw(i, rawValue, header.IndexShift);
        }

        return CsoIndexReadResult.Ok(entries);
    }

    private static long ResolveIndexStart(CsoHeader header)
    {
        if (header.Version == 1)
        {
            return CsoV1IndexStart;
        }

        return header.EffectiveHeaderSize;
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