namespace CsoKit.Core.Formats.Iso;

public static class IsoAlignmentPolicy
{
    public const int SectorSize = 2048;

    public static IsoAlignmentResult Validate(long length, bool allowPadding)
    {
        if (length <= 0)
        {
            return IsoAlignmentResult.Fail("InvalidIsoSize", "ISO file is empty.");
        }

        long remainder = length % SectorSize;

        if (remainder == 0)
        {
            return IsoAlignmentResult.Ok(0);
        }

        long paddingBytes = SectorSize - remainder;

        if (!allowPadding)
        {
            return IsoAlignmentResult.Fail(
                "IsoNotSectorAligned",
                $"ISO size is not aligned to 2048 bytes. Missing padding: {paddingBytes} bytes.");
        }

        return IsoAlignmentResult.Ok(paddingBytes);
    }
}
