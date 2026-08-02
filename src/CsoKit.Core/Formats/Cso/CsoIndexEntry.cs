namespace CsoKit.Core.Formats.Cso;

public sealed record CsoIndexEntry(
    int BlockIndex,
    uint RawValue,
    ulong Offset,
    bool HasFlag)
{
    public const uint OffsetMask = 0x7FFFFFFF;
    public const uint FlagMask = 0x80000000;

    public static CsoIndexEntry FromRaw(int blockIndex, uint rawValue, byte indexShift)
    {
        ulong offset = checked(((ulong)(rawValue & OffsetMask)) << indexShift);
        bool hasFlag = (rawValue & FlagMask) != 0;

        return new CsoIndexEntry(blockIndex, rawValue, offset, hasFlag);
    }
}