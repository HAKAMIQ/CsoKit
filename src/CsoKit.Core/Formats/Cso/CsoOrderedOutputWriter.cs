namespace CsoKit.Core.Formats.Cso;

public sealed class CsoOrderedOutputWriter
{
    private readonly Stream output;

    public CsoOrderedOutputWriter(Stream output)
    {
        ArgumentNullException.ThrowIfNull(output);

        if (!output.CanWrite)
        {
            throw new ArgumentException("CSO output stream must be writable.", nameof(output));
        }

        if (!output.CanSeek)
        {
            throw new ArgumentException("CSO output stream must be seekable.", nameof(output));
        }

        this.output = output;
    }

    public ulong Position => checked((ulong)output.Position);

    public void ReserveDataStart(ulong dataStart)
    {
        if (dataStart > long.MaxValue)
        {
            throw new InvalidDataException("CSO index table is too large.");
        }

        long dataStartPosition = checked((long)dataStart);

        output.SetLength(dataStartPosition);
        output.Position = dataStartPosition;
    }

    public void Write(SectorResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        output.Write(result.OutputSpan);
    }
}