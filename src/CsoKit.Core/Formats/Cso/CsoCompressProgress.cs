namespace CsoKit.Core.Formats.Cso;

public sealed record CsoCompressProgress(
    int CompletedBlocks,
    int TotalBlocks,
    ulong BytesRead,
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

            double percent = BytesRead * 100.0 / TotalBytes;
            return percent > 100 ? 100 : percent;
        }
    }
}