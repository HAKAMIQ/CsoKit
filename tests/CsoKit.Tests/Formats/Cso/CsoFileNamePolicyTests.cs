using CsoKit.Core.Formats.Cso;

namespace CsoKit.Tests.Formats.Cso;

public sealed class CsoFileNamePolicyTests
{
    [Theory]
    [InlineData("A.cso", "OutputFileNameTooShort")]
    [InlineData("ABCDEFGHIJK.cso", "OutputFileNameTooLong")]
    public void TryValidateOutputPath_WithInvalidLength_ReturnsExpectedError(
        string fileName,
        string expectedErrorCode)
    {
        bool valid = CsoFileNamePolicy.TryValidateOutputPath(
            fileName,
            out string? errorCode,
            out string? errorMessage);

        Assert.False(valid);
        Assert.Equal(expectedErrorCode, errorCode);
        Assert.False(string.IsNullOrWhiteSpace(errorMessage));
    }

    [Theory]
    [InlineData("AB.cso")]
    [InlineData("ABCDEFGHIJ.iso")]
    [InlineData("لعبة.cso")]
    public void TryValidateOutputPath_WithLengthFromTwoToTen_ReturnsSuccess(string fileName)
    {
        bool valid = CsoFileNamePolicy.TryValidateOutputPath(
            fileName,
            out string? errorCode,
            out string? errorMessage);

        Assert.True(valid);
        Assert.Null(errorCode);
        Assert.Null(errorMessage);
    }

    [Fact]
    public void NormalizeBaseName_CountsUnicodeTextElements()
    {
        string normalized = CsoFileNamePolicy.NormalizeBaseName("😀😀😀😀😀😀😀😀😀😀😀");

        Assert.Equal("😀😀😀😀😀😀😀😀😀😀", normalized);
    }
}
