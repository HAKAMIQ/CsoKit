using CsoKit.Core.Formats.Iso;

namespace CsoKit.Tests.Formats.Iso;

public sealed class IsoAlignmentPolicyTests
{
    [Fact]
    public void Validate_WithEmptyIso_ReturnsInvalidSize()
    {
        IsoAlignmentResult result = IsoAlignmentPolicy.Validate(0, allowPadding: false);

        Assert.False(result.Success);
        Assert.Equal("InvalidIsoSize", result.ErrorCode);
    }

    [Fact]
    public void Validate_WithUnalignedIsoAndNoPadding_ReturnsIsoNotSectorAligned()
    {
        IsoAlignmentResult result = IsoAlignmentPolicy.Validate(2049, allowPadding: false);

        Assert.False(result.Success);
        Assert.Equal("IsoNotSectorAligned", result.ErrorCode);
        Assert.Equal(0, result.PaddingBytes);
    }

    [Fact]
    public void Validate_WithUnalignedIsoAndPadding_ReturnsRequiredPadding()
    {
        IsoAlignmentResult result = IsoAlignmentPolicy.Validate(2049, allowPadding: true);

        Assert.True(result.Success);
        Assert.Equal(2047, result.PaddingBytes);
    }
}
