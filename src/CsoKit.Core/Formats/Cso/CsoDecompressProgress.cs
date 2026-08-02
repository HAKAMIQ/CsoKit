namespace CsoKit.Core.Formats.Cso;

public sealed record CsoDecompressProgress(
    int CompletedBlocks,
    int TotalBlocks,
    ulong BytesWritten,
    ulong TotalBytes)
{
    public double Percent
    {
        get
        {
            if (TotalBytes == 0)
            {
                return 0;
            }

            double percent = BytesWritten * 100.0 / TotalBytes;
            return percent > 100 ? 100 : percent;
        }
    }
}
