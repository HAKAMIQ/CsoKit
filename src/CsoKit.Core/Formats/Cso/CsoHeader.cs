namespace CsoKit.Core.Formats.Cso;

public sealed record CsoHeader(
    uint HeaderSize,
    ulong UncompressedSize,
    uint BlockSize,
    byte Version,
    byte IndexShift)
{
    public bool IsCsoV1 => Version is 0 or 1;

    public bool IsCsoV2 => Version == 2;

    public bool IsVersionSupported => IsCsoV1 || IsCsoV2;

    public uint EffectiveHeaderSize => IsCsoV1
        ? CsoConstants.MinimumHeaderSize
        : HeaderSize;

    public long SectorCount
    {
        get
        {
            if (BlockSize == 0)
            {
                return 0;
            }

            return checked((long)((UncompressedSize + BlockSize - 1) / BlockSize));
        }
    }

    public long IndexEntryCount => checked(SectorCount + 1);

    public long IndexTableSizeBytes => checked(IndexEntryCount * sizeof(uint));
}
