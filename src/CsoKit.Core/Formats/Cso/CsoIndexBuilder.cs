namespace CsoKit.Core.Formats.Cso;

public sealed class CsoIndexBuilder
{
    private readonly List<uint> entries;
    private readonly byte indexShift;

    public CsoIndexBuilder(int sectorCount, byte indexShift = 0)
    {
        if (sectorCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sectorCount), "Sector count cannot be negative.");
        }

        if (indexShift > CsoConstants.MaxSupportedIndexShift)
        {
            throw new ArgumentOutOfRangeException(nameof(indexShift), "CSO index shift is too large.");
        }

        entries = new List<uint>(checked(sectorCount + 1));
        this.indexShift = indexShift;
    }

    public IReadOnlyList<uint> Entries => entries;

    public void AddSectorOffset(ulong offset, bool isStored)
    {
        entries.Add(EncodeIndexOffset(offset, isStored, indexShift));
    }

    public void AddFinalOffset(ulong offset)
    {
        entries.Add(EncodeIndexOffset(offset, hasFlag: false, indexShift));
    }

    public static uint EncodeIndexOffset(ulong offset, bool hasFlag, byte indexShift = 0)
    {
        return CsoIndexShiftPolicy.EncodeOffset(offset, hasFlag, indexShift);
    }
}
