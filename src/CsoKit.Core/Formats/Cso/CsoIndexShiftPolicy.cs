namespace CsoKit.Core.Formats.Cso;

public static class CsoIndexShiftPolicy
{
    public const uint OffsetMask = 0x7FFF_FFFF;
    public const uint StoredFlag = 0x8000_0000;

    public static byte ComputeShift(ulong worstOffset)
    {
        byte shift = 0;

        while ((worstOffset >> shift) > OffsetMask)
        {
            shift++;

            if (shift > CsoConstants.MaxSupportedIndexShift)
            {
                throw new InvalidDataException("CSO output is too large for the shifted CSO1 index table.");
            }
        }

        return shift;
    }

    public static uint EncodeOffset(ulong offset, bool stored, byte shift)
    {
        ValidateShift(shift);

        ulong alignment = 1UL << shift;

        if (shift > 0 && (offset % alignment) != 0)
        {
            throw new InvalidDataException("CSO offset must be aligned before index encoding.");
        }

        ulong shifted = offset >> shift;

        if (shifted > OffsetMask)
        {
            throw new InvalidDataException("CSO offset does not fit in the CSO index table.");
        }

        uint raw = checked((uint)shifted);

        if (stored)
        {
            raw |= StoredFlag;
        }

        return raw;
    }

    private static void ValidateShift(byte shift)
    {
        if (shift > CsoConstants.MaxSupportedIndexShift)
        {
            throw new InvalidDataException("CSO index shift exceeds the maximum supported value.");
        }

        if (shift >= 64)
        {
            throw new InvalidDataException("CSO index shift must be smaller than 64.");
        }
    }
}