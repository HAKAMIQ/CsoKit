using CsoKit.Core.Formats.Cso;

namespace CsoKit.Tests.Formats.Cso;

public sealed class CsoCompressionProfilePolicyTests
{
    [Theory]
    [InlineData("game-safe", CsoCompressionProfile.GameSafe)]
    [InlineData("compat", CsoCompressionProfile.Compat)]
    [InlineData("FAST", CsoCompressionProfile.Fast)]
    [InlineData("smallest", CsoCompressionProfile.Smallest)]
    [InlineData("archive-smallest", CsoCompressionProfile.ArchiveSmallest)]
    public void TryParse_WithSupportedProfile_ReturnsProfile(
        string value,
        CsoCompressionProfile expected)
    {
        bool success = CsoCompressionProfilePolicy.TryParse(value, out CsoCompressionProfile actual);

        Assert.True(success);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("")]
    [InlineData("unknown")]
    [InlineData("extreme")]
    public void TryParse_WithUnsupportedProfile_ReturnsFalse(string value)
    {
        bool success = CsoCompressionProfilePolicy.TryParse(value, out _);

        Assert.False(success);
    }

    [Fact]
    public void Create_WithFastProfile_UsesFastSettings()
    {
        CsoCompressionProfileSettings settings = CsoCompressionProfilePolicy.Create(CsoCompressionProfile.Fast);

        Assert.Equal("fast", settings.CliName);
        Assert.True(settings.IsFast);
        Assert.Equal(1, settings.Level);
    }

    [Fact]
    public void Create_WithGameSafeProfile_UsesDefaultSafeSettings()
    {
        CsoCompressionProfileSettings settings = CsoCompressionProfilePolicy.Create(CsoCompressionProfile.GameSafe);

        Assert.Equal("game-safe", settings.CliName);
        Assert.False(settings.IsFast);
        Assert.Equal(9, settings.Level);
    }

    [Fact]
    public void Create_WithSmallestProfile_UsesDefaultSafeSettings()
    {
        CsoCompressionProfileSettings settings = CsoCompressionProfilePolicy.Create(CsoCompressionProfile.Smallest);

        Assert.Equal("smallest", settings.CliName);
        Assert.False(settings.IsFast);
        Assert.Equal(9, settings.Level);
    }

    [Fact]
    public void Create_WithCompatProfile_UsesSafeSettings()
    {
        CsoCompressionProfileSettings settings = CsoCompressionProfilePolicy.Create(CsoCompressionProfile.Compat);

        Assert.Equal("compat", settings.CliName);
        Assert.False(settings.IsFast);
        Assert.Equal(9, settings.Level);
    }

    [Fact]
    public void SupportedNamesText_ListsPublicProfilesForHelpAndErrors()
    {
        Assert.Equal("game-safe|compat|fast|smallest|archive-smallest", CsoCompressionProfilePolicy.SupportedNamesText);
    }

    [Theory]
    [InlineData(CsoCompressionProfile.GameSafe, "game-safe")]
    [InlineData(CsoCompressionProfile.Compat, "compat")]
    [InlineData(CsoCompressionProfile.Fast, "fast")]
    [InlineData(CsoCompressionProfile.Smallest, "smallest")]
    [InlineData(CsoCompressionProfile.ArchiveSmallest, "archive-smallest")]
    public void GetCliName_WithSupportedProfile_ReturnsPublicName(
        CsoCompressionProfile profile,
        string expectedName)
    {
        Assert.Equal(expectedName, CsoCompressionProfilePolicy.GetCliName(profile));
    }

}
