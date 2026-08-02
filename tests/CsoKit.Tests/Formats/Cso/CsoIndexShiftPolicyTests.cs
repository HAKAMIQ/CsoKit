using CsoKit.Core.Formats.Cso;

namespace CsoKit.Tests.Formats.Cso;

public sealed class CsoIndexShiftPolicyTests
{
    [Fact]
    public void ComputeShift_WhenOffsetFits31Bits_ReturnsZero()
    {
        byte shift = CsoIndexShiftPolicy.ComputeShift(CsoIndexShiftPolicy.OffsetMask);

        Assert.Equal(0, shift);
    }

    [Fact]
    public void ComputeShift_WhenOffsetExceeds31Bits_ReturnsMinimumShift()
    {
        byte shift = CsoIndexShiftPolicy.ComputeShift((ulong)CsoIndexShiftPolicy.OffsetMask + 1);

        Assert.Equal(1, shift);
    }

    [Fact]
    public void EncodeOffset_WithStoredFlag_SetsHighBit()
    {
        uint encoded = CsoIndexShiftPolicy.EncodeOffset(2048, stored: true, shift: 0);

        Assert.Equal(CsoIndexShiftPolicy.StoredFlag | 2048U, encoded);
    }

    [Fact]
    public void EncodeOffset_WithShiftAndUnalignedOffset_Throws()
    {
        Assert.Throws<InvalidDataException>(() =>
            CsoIndexShiftPolicy.EncodeOffset(3, stored: false, shift: 1));
    }
}
