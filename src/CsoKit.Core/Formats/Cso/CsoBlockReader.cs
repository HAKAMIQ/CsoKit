namespace CsoKit.Core.Formats.Cso;

public static class CsoBlockReader
{
    public static int ReadExactlyOrLess(Stream stream, Span<byte> buffer)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (buffer.IsEmpty)
        {
            return 0;
        }

        return stream.ReadAtLeast(
            buffer,
            buffer.Length,
            throwOnEndOfStream: false);
    }
}