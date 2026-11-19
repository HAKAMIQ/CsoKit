namespace CsoKit.Core.Formats.Containers;

/// <summary>Streams every logical block of a container into a raw ISO stream.</summary>
public static class ContainerIsoDecoder
{
    /// <returns>The number of bytes written, always equal to the container's uncompressed size.</returns>
    public static ulong Decode(
        IBlockContainerReader reader,
        Stream output,
        CancellationToken cancellationToken,
        Action<int, ulong>? blockCompleted = null)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(output);

        byte[] buffer = new byte[checked((int)reader.BlockSize)];
        ulong totalWritten = 0;

        for (int blockIndex = 0; blockIndex < reader.BlockCount; blockIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int bytesRead = reader.ReadBlock(blockIndex, buffer);

            if (bytesRead <= 0)
            {
                throw new BlockContainerReadException(
                    "CorruptCompressedBlock",
                    $"{reader.Format} block {blockIndex} decoded to an empty payload. Re-dump required.",
                    blockIndex);
            }

            output.Write(buffer.AsSpan(0, bytesRead));
            totalWritten += (ulong)bytesRead;
            blockCompleted?.Invoke(blockIndex + 1, totalWritten);
        }

        if (totalWritten != reader.UncompressedSize)
        {
            throw new BlockContainerReadException(
                "ReconstructedSizeMismatch",
                $"{reader.Format} decoded size mismatch. Written {totalWritten:N0} bytes, expected {reader.UncompressedSize:N0} bytes.");
        }

        return totalWritten;
    }
}
