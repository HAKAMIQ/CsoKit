using System.Reflection;
using CsoKit.App.Localization;

namespace CsoKit.App.Tests;

public sealed class UiTextTests
{
    public static TheoryData<string> Languages => new() { "Arabic", "English" };

    [Theory]
    [MemberData(nameof(Languages))]
    public void EveryTextProperty_IsNonEmpty(string language)
    {
        UiText text = GetText(language);

        string[] emptyProperties = typeof(UiText)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.PropertyType == typeof(string))
            .Where(property => string.IsNullOrWhiteSpace((string?)property.GetValue(text)))
            .Select(property => property.Name)
            .ToArray();

        Assert.Empty(emptyProperties);
    }

    [Fact]
    public void Languages_DeclareMatchingLanguageAndDirection()
    {
        Assert.Equal(UiLanguage.Arabic, ArabicUiText.Arabic.Language);
        Assert.Equal("RightToLeft", ArabicUiText.Arabic.FlowDirection.ToString());
        Assert.Equal(UiLanguage.English, ArabicUiText.English.Language);
        Assert.Equal("LeftToRight", ArabicUiText.English.FlowDirection.ToString());
    }

    [Fact]
    public void OperationNames_AreTranslated()
    {
        Assert.NotEqual(ArabicUiText.English.Compress, ArabicUiText.Arabic.Compress);
        Assert.NotEqual(ArabicUiText.English.Decompress, ArabicUiText.Arabic.Decompress);
        Assert.NotEqual(ArabicUiText.English.Verify, ArabicUiText.Arabic.Verify);
        Assert.NotEqual(ArabicUiText.English.Repair, ArabicUiText.Arabic.Repair);
        Assert.NotEqual(ArabicUiText.English.Ready, ArabicUiText.Arabic.Ready);
    }

    private static UiText GetText(string language)
    {
        return language == "Arabic" ? ArabicUiText.Arabic : ArabicUiText.English;
    }
}
