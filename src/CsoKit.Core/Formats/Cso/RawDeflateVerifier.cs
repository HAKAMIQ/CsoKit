using System.IO.Compression;

namespace CsoKit.Core.Formats.Cso;

public static class RawDeflateVerifier
{
    public static bool TryInflate(
        ReadOnlySpan<byte> compressed,
        Span<byte> output,
        out int bytesWritten)
    {
        bytesWritten = 0;

        try
        {
            using MemoryStream input = new(compressed.ToArray(), writable: false);
            using DeflateStream deflate = new(input, CompressionMode.Decompress);

            while (bytesWritten < output.Length)
            {
                int read = deflate.Read(output[bytesWritten..]);

                if (read == 0)
                {
                    break;
                }

                bytesWritten += read;
            }

            if (bytesWritten != output.Length)
            {
                return false;
            }

            Span<byte> extra = stackalloc byte[1];
            return deflate.Read(extra) == 0;
        }
        catch (InvalidDataException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    /// <summary>
    /// Inflates one container block into <paramref name="output"/> (its logical bytes).
    /// A final partial block that the writer zero-filled and compressed as a full
    /// <paramref name="fullBlockSize"/> block (maxcso does this for blocks larger than one
    /// sector) is accepted, and only its logical bytes are kept.
    /// </summary>
    public static bool TryInflateBlock(
        ReadOnlySpan<byte> compressed,
        Span<byte> output,
        int fullBlockSize,
        out int bytesWritten)
    {
        if (TryInflate(compressed, output, out bytesWritten))
        {
            return true;
        }

        if (output.Length >= fullBlockSize)
        {
            return false;
        }

        byte[] fullBlock = new byte[fullBlockSize];

        if (!TryInflate(compressed, fullBlock, out _))
        {
            bytesWritten = 0;
            return false;
        }

        fullBlock.AsSpan(0, output.Length).CopyTo(output);
        bytesWritten = output.Length;
        return true;
    }

    public static bool RoundtripEquals(
        ReadOnlySpan<byte> compressed,
        ReadOnlySpan<byte> original,
        int expectedBytes)
    {
        byte[] restored = new byte[expectedBytes];

        return TryInflate(compressed, restored, out int bytesWritten) &&
            bytesWritten == expectedBytes &&
            restored.AsSpan().SequenceEqual(original);
    }
}