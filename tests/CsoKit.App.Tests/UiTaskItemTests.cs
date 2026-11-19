using CsoKit.App.Models;

namespace CsoKit.App.Tests;

public sealed class UiTaskItemTests
{
    [Theory]
    [InlineData("game.iso", "ISO")]
    [InlineData("game.CSO", "CSO")]
    [InlineData("game.chd", "CHD")]
    [InlineData("game.zso", "ZSO")]
    [InlineData("game.dax", "Other")]
    [InlineData("game", "Unknown")]
    public void Create_ClassifiesMediaKindByExtension(string fileName, string expectedKind)
    {
        UiTaskItem task = UiTaskItem.Create(Path.Combine(Path.GetTempPath(), "csokit-missing", fileName), "ready");

        Assert.Equal(expectedKind, task.MediaKind);
        Assert.Equal("game", task.DisplayName);
        Assert.Equal("ready", task.UserFacingStatus);
    }

    [Fact]
    public void Create_ForExistingDirectory_UsesFolderKindAndDirectoryName()
    {
        using TempDirectory temp = new();
        string folder = Directory.CreateDirectory(temp.Combine("games")).FullName;

        UiTaskItem task = UiTaskItem.Create(folder, "ready");

        Assert.Equal("Folder", task.MediaKind);
        Assert.Equal("games", task.DisplayName);
    }

    [Fact]
    public void SetStatus_RaisesPropertyChangedOnlyWhenValueChanges()
    {
        UiTaskItem task = UiTaskItem.Create("game.iso", "ready");
        List<string?> changed = [];
        task.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        task.SetStatus("ready");
        task.SetStatus("running");

        Assert.Equal([nameof(UiTaskItem.UserFacingStatus)], changed);
        Assert.Equal("running", task.UserFacingStatus);
    }
}
