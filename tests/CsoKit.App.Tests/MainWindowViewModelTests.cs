using CsoKit.App.Localization;
using CsoKit.App.Models;
using CsoKit.App.ViewModels;

namespace CsoKit.App.Tests;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public void NewViewModel_StartsReadyInArabicWithCompressDefaults()
    {
        MainWindowViewModel viewModel = new();

        Assert.Equal(UiLanguage.Arabic, viewModel.Text.Language);
        Assert.Equal(UiOperationKind.Compress, viewModel.SelectedOperation);
        Assert.Equal("game-safe", viewModel.SelectedProfileName);
        Assert.Equal(ArabicUiText.Arabic.Ready, viewModel.StatusText);
        Assert.False(viewModel.IsBusy);
        Assert.True(viewModel.CanEdit);
        Assert.False(viewModel.CanCancel);
        Assert.False(viewModel.CanOpenOutput);
        Assert.True(viewModel.RequiresOutputPath);
        Assert.Empty(viewModel.Tasks);
        Assert.Empty(viewModel.LogEntries);
    }

    [Fact]
    public void ToggleLanguage_SwitchesTextAndLocalizedStatus()
    {
        MainWindowViewModel viewModel = new();

        viewModel.ToggleLanguage();

        Assert.Equal(UiLanguage.English, viewModel.Text.Language);
        Assert.Equal("العربية", viewModel.LanguageToggleText);
        Assert.Equal(ArabicUiText.English.Ready, viewModel.StatusText);
        Assert.Equal(ArabicUiText.English.Compress, viewModel.SelectedOperationName);
        Assert.Equal(ArabicUiText.English.NoResultsYet, viewModel.Summary.Message);

        viewModel.ToggleLanguage();

        Assert.Equal(UiLanguage.Arabic, viewModel.Text.Language);
        Assert.Equal("English", viewModel.LanguageToggleText);
    }

    [Fact]
    public void SetInputPathFromUser_AddsTaskAndSuggestsSiblingCsoOutput()
    {
        using TempDirectory temp = new();
        string input = temp.CreateFile("game.iso");
        MainWindowViewModel viewModel = new();

        viewModel.SetInputPathFromUser(input);

        UiTaskItem task = Assert.Single(viewModel.Tasks);
        Assert.Same(task, viewModel.SelectedTask);
        Assert.Equal("ISO", task.MediaKind);
        Assert.Equal(input, viewModel.InputPath);
        Assert.Equal(temp.Combine("game.cso"), viewModel.OutputPath);
        Assert.Equal("game.cso", viewModel.CompactOutputText);
        Assert.Single(viewModel.LogEntries);
    }

    [Fact]
    public void SetInputPathsFromUser_IgnoresBlanksAndCaseInsensitiveDuplicates()
    {
        using TempDirectory temp = new();
        string first = temp.CreateFile("first.iso");
        string second = temp.CreateFile("second.iso");
        MainWindowViewModel viewModel = new();

        viewModel.SetInputPathsFromUser([first, "  ", first.ToUpperInvariant(), second, string.Empty]);
        viewModel.SetInputPathsFromUser([second]);

        Assert.Equal(2, viewModel.Tasks.Count);
        Assert.Equal(first, viewModel.Tasks[0].Path);
        Assert.Equal(second, viewModel.Tasks[1].Path);
        Assert.Equal(second, viewModel.InputPath);
    }

    [Fact]
    public void ChangingOperation_RefreshesSuggestedOutputExtension()
    {
        using TempDirectory temp = new();
        string input = temp.CreateFile("game.cso");
        MainWindowViewModel viewModel = new()
        {
            SelectedOperation = UiOperationKind.Decompress,
        };

        viewModel.SetInputPathFromUser(input);

        Assert.Equal(temp.Combine("game.iso"), viewModel.OutputPath);

        viewModel.SelectedOperation = UiOperationKind.Repair;

        Assert.Equal(".cso", Path.GetExtension(viewModel.OutputPath));
        Assert.NotEqual(input, viewModel.OutputPath);
    }

    [Fact]
    public void UserEditedOutputPath_IsKeptWhenOperationChanges()
    {
        using TempDirectory temp = new();
        string input = temp.CreateFile("game.iso");
        string custom = temp.Combine("mine.cso");
        MainWindowViewModel viewModel = new();
        viewModel.SetInputPathFromUser(input);

        viewModel.SetOutputPathFromUser(custom);
        viewModel.SelectedOperation = UiOperationKind.Repair;

        Assert.Equal(custom, viewModel.OutputPath);
    }

    [Fact]
    public void SelectingAnotherTask_ResetsUserEditedOutputPath()
    {
        using TempDirectory temp = new();
        string first = temp.CreateFile("first.iso");
        string second = temp.CreateFile("second.iso");
        MainWindowViewModel viewModel = new();
        viewModel.SetInputPathsFromUser([first, second]);
        viewModel.SetOutputPathFromUser(temp.Combine("mine.cso"));

        viewModel.SelectedTask = viewModel.Tasks[1];

        Assert.Equal(second, viewModel.InputPath);
        Assert.Equal(temp.Combine("second.cso"), viewModel.OutputPath);
    }

    [Theory]
    [InlineData(UiOperationKind.Compress, true, true, true, true, true, false, true)]
    [InlineData(UiOperationKind.Measure, false, true, true, false, false, false, false)]
    [InlineData(UiOperationKind.Decompress, true, false, false, false, false, false, false)]
    [InlineData(UiOperationKind.Verify, false, false, false, false, true, true, false)]
    [InlineData(UiOperationKind.Repair, true, false, false, false, false, false, true)]
    [InlineData(UiOperationKind.Detect, false, false, false, false, false, false, false)]
    [InlineData(UiOperationKind.Analyze, false, false, false, false, false, false, false)]
    public void OptionAvailability_MatchesOperation(
        UiOperationKind operation,
        bool requiresOutput,
        bool profile,
        bool blockSize,
        bool workers,
        bool deepVerify,
        bool sha256,
        bool codecReport)
    {
        MainWindowViewModel viewModel = new() { SelectedOperation = operation };

        Assert.Equal(requiresOutput, viewModel.RequiresOutputPath);
        Assert.Equal(requiresOutput, viewModel.CanBrowseOutput);
        Assert.Equal(requiresOutput, viewModel.CanUseForceOverwrite);
        Assert.Equal(profile, viewModel.CanUseProfile);
        Assert.Equal(blockSize, viewModel.CanUseBlockSize);
        Assert.Equal(workers, viewModel.CanUseWorkerCount);
        Assert.Equal(deepVerify, viewModel.CanUseDeepVerify);
        Assert.Equal(sha256, viewModel.CanUseSha256);
        Assert.Equal(codecReport, viewModel.CanUseCodecReport);
    }

    [Theory]
    [InlineData(UiOperationKind.Verify)]
    [InlineData(UiOperationKind.Repair)]
    public void VerifyAndRepair_EnableDeepVerifyByDefault(UiOperationKind operation)
    {
        MainWindowViewModel viewModel = new() { DeepVerify = false };

        viewModel.SelectedOperation = operation;

        Assert.True(viewModel.DeepVerify);
    }

    [Fact]
    public void Repair_ResetsProfileToGameSafe()
    {
        MainWindowViewModel viewModel = new() { SelectedProfileName = "fast" };

        viewModel.SelectedOperation = UiOperationKind.Repair;

        Assert.Equal("game-safe", viewModel.SelectedProfileName);
    }

    [Fact]
    public async Task Run_WithoutInput_FailsWithInputRequiredMessage()
    {
        MainWindowViewModel viewModel = new();

        await viewModel.RunSelectedOperationAsync();

        AssertFailedWith(viewModel, ArabicUiText.Arabic.InputPathRequired);
    }

    [Fact]
    public async Task Run_Compress_WithInvalidBlockSize_FailsBeforeStarting()
    {
        using TempDirectory temp = new();
        MainWindowViewModel viewModel = new() { BlockSizeText = "0" };
        viewModel.SetInputPathFromUser(temp.CreateFile("game.iso"));

        await viewModel.RunSelectedOperationAsync();

        AssertFailedWith(viewModel, ArabicUiText.Arabic.InvalidBlockSize);
        Assert.False(File.Exists(temp.Combine("game.cso")));
    }

    [Fact]
    public async Task Run_Compress_WithUnknownProfile_FailsBeforeStarting()
    {
        using TempDirectory temp = new();
        MainWindowViewModel viewModel = new() { SelectedProfileName = "no-such-profile" };
        viewModel.SetInputPathFromUser(temp.CreateFile("game.iso"));

        await viewModel.RunSelectedOperationAsync();

        AssertFailedWith(viewModel, ArabicUiText.Arabic.InvalidCompressionProfile);
    }

    [Fact]
    public async Task Run_Compress_WithNonNumericWorkerCount_FailsBeforeStarting()
    {
        using TempDirectory temp = new();
        MainWindowViewModel viewModel = new() { WorkerCountText = "many" };
        viewModel.SetInputPathFromUser(temp.CreateFile("game.iso"));

        await viewModel.RunSelectedOperationAsync();

        AssertFailedWith(viewModel, ArabicUiText.Arabic.InvalidThreads);
    }

    [Fact]
    public async Task Run_Compress_WithTooLongOutputName_FailsBeforeStarting()
    {
        using TempDirectory temp = new();
        MainWindowViewModel viewModel = new();
        viewModel.SetInputPathFromUser(temp.CreateFile("game.iso"));
        viewModel.SetOutputPathFromUser(temp.Combine("verylongname.cso"));

        await viewModel.RunSelectedOperationAsync();

        AssertFailedWith(viewModel, ArabicUiText.Arabic.InvalidOutputFileNameLength);
        Assert.False(File.Exists(temp.Combine("verylongname.cso")));
    }

    [Fact]
    public async Task Run_Detect_WritesReportAndOffersItAsOpenTarget()
    {
        using TempDirectory temp = new();
        string input = temp.CreateCsoHeaderFile("game.cso");
        MainWindowViewModel viewModel = new() { SelectedOperation = UiOperationKind.Detect };
        viewModel.SetInputPathFromUser(input);

        await viewModel.RunSelectedOperationAsync();

        Assert.False(viewModel.IsBusy);
        Assert.False(viewModel.CanCancel);
        Assert.Equal(ArabicUiText.Arabic.OperationSucceeded, viewModel.StatusText);
        Assert.Equal(ArabicUiText.Arabic.Completed, viewModel.CurrentStageText);
        Assert.Equal(100, viewModel.ProgressValue);
        Assert.Equal(ArabicUiText.Arabic.OperationSucceeded, viewModel.Summary.Title);
        Assert.Equal(ArabicUiText.Arabic.Detect, viewModel.Summary.Message);
        Assert.NotEqual("-", viewModel.Summary.OriginalSizeText);

        string expectedReport = temp.Combine("game.detect-report.txt");
        Assert.True(File.Exists(expectedReport));
        Assert.True(viewModel.CanOpenOutput);
        Assert.Equal(expectedReport, viewModel.OpenTargetPath);
        Assert.Equal("فتح التقرير", viewModel.OpenResultText);
        Assert.Equal(ArabicUiText.Arabic.Completed, viewModel.Tasks[0].UserFacingStatus);
        Assert.Contains(viewModel.LogEntries, entry => entry.Kind == "Success");
    }

    [Fact]
    public async Task Run_Detect_OnMissingFile_ReportsFailure()
    {
        using TempDirectory temp = new();
        MainWindowViewModel viewModel = new() { SelectedOperation = UiOperationKind.Detect };
        viewModel.InputPath = temp.Combine("missing.iso");

        await viewModel.RunSelectedOperationAsync();

        Assert.False(viewModel.IsBusy);
        Assert.Equal(ArabicUiText.Arabic.OperationFailed, viewModel.StatusText);
        Assert.Equal(ArabicUiText.Arabic.OperationFailed, viewModel.Summary.Title);
        Assert.Equal(0, viewModel.ProgressValue);
        Assert.Contains(viewModel.LogEntries, entry => entry.Kind == "Error");
    }

    [Fact]
    public async Task CompletedStatus_IsRelocalizedWhenLanguageChanges()
    {
        using TempDirectory temp = new();
        MainWindowViewModel viewModel = new() { SelectedOperation = UiOperationKind.Detect };
        viewModel.SetInputPathFromUser(temp.CreateCsoHeaderFile("game.cso"));
        await viewModel.RunSelectedOperationAsync();

        viewModel.ToggleLanguage();

        Assert.Equal(ArabicUiText.English.OperationSucceeded, viewModel.StatusText);
        Assert.Equal(ArabicUiText.English.Completed, viewModel.CurrentStageText);
        Assert.Equal("Open report", viewModel.OpenResultText);
    }

    [Fact]
    public async Task ClearLog_ResetsCompletedOperationState()
    {
        using TempDirectory temp = new();
        MainWindowViewModel viewModel = new() { SelectedOperation = UiOperationKind.Detect };
        viewModel.SetInputPathFromUser(temp.CreateCsoHeaderFile("game.cso"));
        await viewModel.RunSelectedOperationAsync();

        viewModel.ClearLog();

        Assert.Empty(viewModel.Tasks);
        Assert.Empty(viewModel.LogEntries);
        Assert.Null(viewModel.SelectedTask);
        Assert.False(viewModel.CanOpenOutput);
        Assert.Equal(string.Empty, viewModel.OpenTargetPath);
        Assert.Equal(0, viewModel.ProgressValue);
        Assert.Equal(ArabicUiText.Arabic.Ready, viewModel.StatusText);
        Assert.Equal(ArabicUiText.Arabic.NoResultsYet, viewModel.Summary.Message);
    }

    [Fact]
    public void CancelCurrentOperation_WhenIdle_DoesNothing()
    {
        MainWindowViewModel viewModel = new();

        viewModel.CancelCurrentOperation();

        Assert.Equal(ArabicUiText.Arabic.Ready, viewModel.CurrentStageText);
        Assert.False(viewModel.CanCancel);
    }

    private static void AssertFailedWith(MainWindowViewModel viewModel, string expectedMessage)
    {
        Assert.False(viewModel.IsBusy);
        Assert.True(viewModel.CanEdit);
        Assert.Equal(ArabicUiText.Arabic.OperationFailed, viewModel.StatusText);
        Assert.Equal(ArabicUiText.Arabic.OperationFailed, viewModel.Summary.Title);
        Assert.Equal(expectedMessage, viewModel.Summary.Message);
        Assert.False(viewModel.CanOpenOutput);
        Assert.Contains(viewModel.LogEntries, entry => entry.Kind == "Error" && entry.Message == expectedMessage);
    }
}
