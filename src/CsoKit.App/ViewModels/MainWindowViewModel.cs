using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using CsoKit.App.Localization;
using CsoKit.App.Models;
using CsoKit.App.Services;
using CsoKit.Application;
using CsoKit.Core.Compression;
using CsoKit.Core.Formats.Cso;

namespace CsoKit.App.ViewModels;

public sealed partial class MainWindowViewModel : INotifyPropertyChanged
{
    private UiText text = ArabicUiText.Arabic;
    private string inputPath = string.Empty;
    private string outputPath = string.Empty;
    private bool outputPathWasEditedByUser;
    private bool isSettingOutputPathInternally;
    private UiOperationKind selectedOperation = UiOperationKind.Compress;
    private string selectedProfileName = "game-safe";
    private string blockSizeText = "2048";
    private string workerCountText = "1";
    private bool forceOverwrite;
    private bool deepVerify;
    private bool computeSha256;
    private bool collectCodecReport;
    private bool isBusy;
    private bool isAdvancedOptionsOpen;
    private bool canOpenOutput;
    private string openTargetPath = string.Empty;
    private bool openTargetIsReport = true;
    private string statusText = ArabicUiText.Arabic.Ready;
    private string currentStageText = ArabicUiText.Arabic.Ready;
    private double progressValue;
    private bool isProgressIndeterminate;
    private UiTaskItem? selectedTask;
    private UiOperationKind? lastCompletedOperationKind;
    private bool hasCompletedOperation;
    private bool lastCompletedOperationSucceeded;
    private OperationSummaryViewModel summary = OperationSummaryViewModel.CreateEmpty(ArabicUiText.Arabic);
    private CancellationTokenSource? operationCancellation;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<UiLogEntry> LogEntries { get; } = [];

    public ObservableCollection<UiTaskItem> Tasks { get; } = [];

    public UiText Text
    {
        get => text;
        private set
        {
            if (SetField(ref text, value))
            {
                OnPropertyChanged(nameof(LanguageToggleText));
                OnPropertyChanged(nameof(SelectedOperationName));
                OnPropertyChanged(nameof(SelectedOperationDescription));
                OnPropertyChanged(nameof(OutputRequirementText));
                OnPropertyChanged(nameof(CompactOutputText));
                OnPropertyChanged(nameof(OpenResultText));
                RefreshTaskStatuses();
                RefreshLocalizedStatusText();
            }
        }
    }

    public string LanguageToggleText => Text.Language == UiLanguage.Arabic ? "English" : "العربية";

    public string InputPath
    {
        get => inputPath;
        set
        {
            string normalized = value?.Trim() ?? string.Empty;

            if (SetField(ref inputPath, normalized))
            {
                RefreshSuggestedOutputPath();
            }
        }
    }

    public string OutputPath
    {
        get => outputPath;
        set
        {
            string normalized = value?.Trim() ?? string.Empty;

            if (SetField(ref outputPath, normalized) && !isSettingOutputPathInternally)
            {
                outputPathWasEditedByUser = !string.IsNullOrWhiteSpace(normalized);
            }

            ClearOpenTarget();
            OnPropertyChanged(nameof(CompactOutputText));
        }
    }

    public UiOperationKind SelectedOperation
    {
        get => selectedOperation;
        set
        {
            if (SetField(ref selectedOperation, value))
            {
                ApplyOperationDefaults();
                OnOperationPresentationChanged();
                RefreshSuggestedOutputPath();
            }
        }
    }

    public string SelectedProfileName
    {
        get => selectedProfileName;
        set => SetField(ref selectedProfileName, value?.Trim() ?? string.Empty);
    }

    public string BlockSizeText
    {
        get => blockSizeText;
        set => SetField(ref blockSizeText, value?.Trim() ?? string.Empty);
    }

    public string WorkerCountText
    {
        get => workerCountText;
        set => SetField(ref workerCountText, value?.Trim() ?? string.Empty);
    }

    public bool ForceOverwrite
    {
        get => forceOverwrite;
        set => SetField(ref forceOverwrite, value);
    }

    public bool DeepVerify
    {
        get => deepVerify;
        set => SetField(ref deepVerify, value);
    }

    public bool ComputeSha256
    {
        get => computeSha256;
        set => SetField(ref computeSha256, value);
    }

    public bool CollectCodecReport
    {
        get => collectCodecReport;
        set => SetField(ref collectCodecReport, value);
    }

    public bool IsBusy
    {
        get => isBusy;
        private set
        {
            if (SetField(ref isBusy, value))
            {
                OnPropertyChanged(nameof(CanEdit));
                OnPropertyChanged(nameof(CanCancel));
                OnOptionAvailabilityChanged();
            }
        }
    }

    public bool CanEdit => !IsBusy;

    public bool CanCancel => IsBusy && operationCancellation is { IsCancellationRequested: false };

    public bool RequiresOutputPath => SelectedOperation is UiOperationKind.Compress or UiOperationKind.Decompress or UiOperationKind.Repair;

    public bool CanBrowseOutput => CanEdit && RequiresOutputPath;

    public bool CanUseProfile => CanEdit && SelectedOperationUsesProfile;

    public bool CanUseForceOverwrite => CanEdit && RequiresOutputPath;

    public bool CanUseWorkerCount => CanEdit && SelectedOperationUsesWorkerCount;

    public bool CanUseBlockSize => CanEdit && SelectedOperationUsesBlockSize;

    public bool CanUseDeepVerify => CanEdit && SelectedOperationCanToggleDeepVerify;

    public bool CanUseSha256 => CanEdit && SelectedOperation is UiOperationKind.Verify;

    public bool CanUseCodecReport => CanEdit && SelectedOperationUsesCodecReport;

    public bool CanOpenOutput => !IsBusy && canOpenOutput;

    public string OpenTargetPath => openTargetPath;

    public string OpenResultText => openTargetIsReport
        ? Text.Language == UiLanguage.Arabic ? "فتح التقرير" : "Open report"
        : Text.OpenOutput;

    public bool IsAdvancedOptionsOpen
    {
        get => isAdvancedOptionsOpen;
        set => SetField(ref isAdvancedOptionsOpen, value);
    }

    public UiTaskItem? SelectedTask
    {
        get => selectedTask;
        set
        {
            if (SetField(ref selectedTask, value) && value is not null)
            {
                SetInputPathFromTask(value);
            }
        }
    }

    public string CompactOutputText => RequiresOutputPath && !string.IsNullOrWhiteSpace(OutputPath)
        ? Path.GetFileName(OutputPath)
        : Text.AutomaticOutput;

    public string SelectedOperationName => GetOperationName(SelectedOperation);

    public string SelectedOperationDescription => GetOperationDescription(SelectedOperation);

    public string OutputRequirementText => RequiresOutputPath
        ? Text.OutputFileHint
        : Text.OutputPathNotRequired;

    public string StatusText
    {
        get => statusText;
        private set => SetField(ref statusText, value);
    }

    public string CurrentStageText
    {
        get => currentStageText;
        private set => SetField(ref currentStageText, value);
    }

    public double ProgressValue
    {
        get => progressValue;
        private set => SetField(ref progressValue, value);
    }

    public bool IsProgressIndeterminate
    {
        get => isProgressIndeterminate;
        private set => SetField(ref isProgressIndeterminate, value);
    }

    public OperationSummaryViewModel Summary
    {
        get => summary;
        private set => SetField(ref summary, value);
    }

    public void ToggleLanguage()
    {
        Text = Text.Language == UiLanguage.Arabic
            ? ArabicUiText.English
            : ArabicUiText.Arabic;

        if (!IsBusy && LogEntries.Count == 0)
        {
            Summary = OperationSummaryViewModel.CreateEmpty(Text);
        }
    }

    public void ClearLog()
    {
        LogEntries.Clear();
        Tasks.Clear();
        selectedTask = null;
        hasCompletedOperation = false;
        lastCompletedOperationKind = null;
        lastCompletedOperationSucceeded = false;
        OnPropertyChanged(nameof(SelectedTask));
        ClearOpenTarget();
        StatusText = Text.Ready;
        CurrentStageText = Text.Ready;
        ProgressValue = 0;
        IsProgressIndeterminate = false;
        Summary = OperationSummaryViewModel.CreateEmpty(Text);
    }

    public void SetInputPathFromUser(string path)
    {
        SetInputPathsFromUser(new[] { path });
    }

    public void SetInputPathsFromUser(IEnumerable<string> paths)
    {
        string[] normalizedPaths = paths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalizedPaths.Length == 0)
        {
            return;
        }

        outputPathWasEditedByUser = false;
        ClearOpenTarget();

        UiTaskItem? taskToSelect = null;

        foreach (string path in normalizedPaths)
        {
            UiTaskItem? existingTask = Tasks.FirstOrDefault(task =>
                string.Equals(task.Path, path, StringComparison.OrdinalIgnoreCase));

            if (existingTask is not null)
            {
                existingTask.SetStatus(CreateTaskStatusForCurrentOperation(existingTask.Path));
                taskToSelect ??= existingTask;
                continue;
            }

            UiTaskItem task = UiTaskItem.Create(path, CreateTaskStatusForCurrentOperation(path));
            Tasks.Add(task);
            taskToSelect ??= task;
            AddLog(Text.Input, path, "Info");
        }

        if (taskToSelect is not null)
        {
            SelectedTask = taskToSelect;
        }
    }

    public void SetOutputPathFromUser(string path)
    {
        OutputPath = path;
        ClearOpenTarget();
        outputPathWasEditedByUser = !string.IsNullOrWhiteSpace(path);

        if (!string.IsNullOrWhiteSpace(OutputPath))
        {
            AddLog(Text.Output, OutputPath, "Info");
        }
    }

    public void ToggleAdvancedOptions()
    {
        IsAdvancedOptionsOpen = !IsAdvancedOptionsOpen;
    }

    public string GetSuggestedOutputPath()
    {
        return CreateSuggestedOutputPath(InputPath, SelectedOperation);
    }

    private string? TryWriteOperationReport(
        OperationRequest request,
        CsoOperationResult result,
        string operationName)
    {
        OperationReportWriteResult write = OperationReportService.TryWrite(
            new OperationReportRequest(request.Kind, request.InputPath, request.OutputPath),
            result,
            operationName,
            Text.Language);

        if (write.Success)
        {
            AddLog(Text.Language == UiLanguage.Arabic ? "التقرير" : "Report", write.ReportPath!, "Info");
            return write.ReportPath;
        }

        if (!string.IsNullOrWhiteSpace(write.ErrorMessage))
        {
            AddLog(Text.Error, write.ErrorMessage, "Error");
        }

        return null;
    }

    private static OperationOpenTarget? ResolveOpenTarget(OperationRequest request, string? reportPath)
    {
        if (ShouldPreferReportTarget(request.Kind) && IsExistingFile(reportPath))
        {
            return new OperationOpenTarget(reportPath!, IsReport: true);
        }

        if (request.Kind is UiOperationKind.Compress or UiOperationKind.Decompress && IsExistingFile(request.OutputPath))
        {
            return new OperationOpenTarget(request.OutputPath, IsReport: false);
        }

        if (IsExistingFile(reportPath))
        {
            return new OperationOpenTarget(reportPath!, IsReport: true);
        }

        return null;
    }

    private static bool ShouldPreferReportTarget(UiOperationKind operationKind)
    {
        return operationKind is UiOperationKind.Verify or UiOperationKind.Repair or UiOperationKind.Detect or UiOperationKind.Analyze or UiOperationKind.Measure;
    }

    private static bool IsExistingFile(string? path)
    {
        return !string.IsNullOrWhiteSpace(path) && File.Exists(path);
    }

    private string SimplifyDetailsForUser(string details)
    {
        if (string.IsNullOrWhiteSpace(details))
        {
            return string.Empty;
        }

        return details
            .Replace("Input:", $"{Text.Input}:", StringComparison.Ordinal)
            .Replace("Output:", $"{Text.Output}:", StringComparison.Ordinal)
            .Replace("Format:", "Format:", StringComparison.Ordinal)
            .Replace("Error:", $"{Text.Error}:", StringComparison.Ordinal)
            .Replace("Warnings:", "Warnings:", StringComparison.Ordinal)
            .Trim();
    }

    private void AddLog(string title, string message, string kind)
    {
        LogEntries.Add(new UiLogEntry(title, message, kind));
    }

    private string BuildOperationStartMessage(OperationRequest request, string operationName)
    {
        if (string.IsNullOrWhiteSpace(request.OutputPath))
        {
            return $"{operationName}\n{Text.Input}: {request.InputPath}";
        }

        return $"{operationName}\n{Text.Input}: {request.InputPath}\n{Text.Output}: {request.OutputPath}";
    }

    private string GetOperationName(UiOperationKind operationKind)
    {
        return operationKind switch
        {
            UiOperationKind.Compress => Text.Compress,
            UiOperationKind.Detect => Text.Detect,
            UiOperationKind.Analyze => Text.Analyze,
            UiOperationKind.Measure => Text.Measure,
            UiOperationKind.Verify => Text.Verify,
            UiOperationKind.Decompress => Text.Decompress,
            UiOperationKind.Repair => Text.Repair,
            _ => throw new ArgumentOutOfRangeException(nameof(operationKind), operationKind, Text.UnsupportedOperation),
        };
    }

    private string GetOperationDescription(UiOperationKind operationKind)
    {
        return operationKind switch
        {
            UiOperationKind.Compress => Text.CompressDescription,
            UiOperationKind.Detect => Text.DetectDescription,
            UiOperationKind.Analyze => Text.AnalyzeDescription,
            UiOperationKind.Measure => Text.MeasureDescription,
            UiOperationKind.Verify => Text.VerifyDescription,
            UiOperationKind.Decompress => Text.DecompressDescription,
            UiOperationKind.Repair => Text.RepairDescription,
            _ => Text.UnsupportedOperation,
        };
    }

    private bool SelectedOperationUsesProfile => SelectedOperation is UiOperationKind.Compress or UiOperationKind.Measure;

    private bool SelectedOperationUsesBlockSize => SelectedOperation is UiOperationKind.Compress or UiOperationKind.Measure;

    private bool SelectedOperationUsesWorkerCount => SelectedOperation is UiOperationKind.Compress;

    private bool SelectedOperationUsesDeepVerify => SelectedOperation is UiOperationKind.Compress or UiOperationKind.Verify or UiOperationKind.Repair;

    private bool SelectedOperationCanToggleDeepVerify => SelectedOperation is UiOperationKind.Compress or UiOperationKind.Verify;

    private bool SelectedOperationUsesCodecReport => SelectedOperation is UiOperationKind.Compress or UiOperationKind.Repair;

    private void OnOperationPresentationChanged()
    {
        ClearOpenTarget();
        OnPropertyChanged(nameof(RequiresOutputPath));
        OnPropertyChanged(nameof(SelectedOperationName));
        OnPropertyChanged(nameof(SelectedOperationDescription));
        OnPropertyChanged(nameof(OutputRequirementText));
        OnPropertyChanged(nameof(CompactOutputText));
        OnPropertyChanged(nameof(CanOpenOutput));
        RefreshTaskStatuses();
        OnOptionAvailabilityChanged();
    }

    private void ApplyOperationDefaults()
    {
        if (SelectedOperation is UiOperationKind.Verify or UiOperationKind.Repair)
        {
            DeepVerify = true;
        }

        if (SelectedOperation is UiOperationKind.Repair)
        {
            SelectedProfileName = "game-safe";
        }
    }

    private void OnOptionAvailabilityChanged()
    {
        OnPropertyChanged(nameof(CanBrowseOutput));
        OnPropertyChanged(nameof(CanOpenOutput));
        OnPropertyChanged(nameof(CanUseProfile));
        OnPropertyChanged(nameof(CanUseForceOverwrite));
        OnPropertyChanged(nameof(CanUseWorkerCount));
        OnPropertyChanged(nameof(CanUseBlockSize));
        OnPropertyChanged(nameof(CanUseDeepVerify));
        OnPropertyChanged(nameof(CanUseSha256));
        OnPropertyChanged(nameof(CanUseCodecReport));
    }

    private void SetOpenTarget(string path, bool isReport)
    {
        string normalizedPath = path.Trim();
        openTargetPath = normalizedPath;
        openTargetIsReport = isReport;
        canOpenOutput = File.Exists(normalizedPath) || Directory.Exists(normalizedPath);
        OnPropertyChanged(nameof(OpenTargetPath));
        OnPropertyChanged(nameof(OpenResultText));
        OnPropertyChanged(nameof(CanOpenOutput));
    }

    private void ClearOpenTarget()
    {
        openTargetPath = string.Empty;
        openTargetIsReport = true;
        canOpenOutput = false;
        OnPropertyChanged(nameof(OpenTargetPath));
        OnPropertyChanged(nameof(OpenResultText));
        OnPropertyChanged(nameof(CanOpenOutput));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void SetInputPathFromTask(UiTaskItem task)
    {
        outputPathWasEditedByUser = false;
        InputPath = task.Path;
    }

    private void RefreshTaskStatuses()
    {
        foreach (UiTaskItem task in Tasks)
        {
            task.SetStatus(CreateTaskStatusForCurrentOperation(task.Path));
        }
    }

    private string CreateTaskStatusForCurrentOperation(string path)
    {
        string kind = Directory.Exists(path)
            ? "Folder"
            : GetMediaKind(Path.GetExtension(path));
        string action = SelectedOperation switch
        {
            UiOperationKind.Compress => Text.ReadyToCompress,
            UiOperationKind.Decompress => Text.ReadyToExtractIso,
            UiOperationKind.Verify => Text.ReadyToVerify,
            UiOperationKind.Repair => Text.ReadyToRepair,
            UiOperationKind.Detect => Text.ReadyToDetect,
            UiOperationKind.Analyze => Text.ReadyToAnalyze,
            UiOperationKind.Measure => Text.ReadyToMeasure,
            _ => Text.Ready,
        };

        return $"{kind} · {action}";
    }

    private string CreatePreparingStageText(UiOperationKind operationKind)
    {
        return operationKind switch
        {
            UiOperationKind.Compress => Text.PreparingDetectInput,
            UiOperationKind.Decompress => Text.PreparingReadHeader,
            UiOperationKind.Verify => Text.PreparingCheckIndex,
            UiOperationKind.Repair => Text.PreparingRepair,
            UiOperationKind.Detect => Text.PreparingReadHeader,
            UiOperationKind.Analyze => Text.PreparingAnalyzeImage,
            UiOperationKind.Measure => Text.PreparingMeasure,
            _ => Text.Running,
        };
    }

    private string CreateProgressStageText(UiOperationKind operationKind, double progress)
    {
        if (progress < 8)
        {
            return Text.ProgressReadHeader;
        }

        if (progress < 24)
        {
            return Text.ProgressCheckIndex;
        }

        if (progress < 92)
        {
            return operationKind switch
            {
                UiOperationKind.Compress => Text.ProgressWriteCso,
                UiOperationKind.Decompress => Text.ProgressWriteIso,
                UiOperationKind.Repair => Text.ProgressRepair,
                UiOperationKind.Verify => Text.ProgressVerifyData,
                UiOperationKind.Measure => Text.ProgressMeasure,
                _ => Text.ProgressRunningOperation,
            };
        }

        return Text.ProgressCheckingOutput;
    }

    private string CreateCompletedStageText(UiOperationKind operationKind)
    {
        return operationKind switch
        {
            UiOperationKind.Repair => Text.RepairCompleted,
            UiOperationKind.Verify => Text.VerifyCompleted,
            UiOperationKind.Compress or UiOperationKind.Decompress => Text.WriteCompleted,
            _ => Text.Completed,
        };
    }

    private void RefreshLocalizedStatusText()
    {
        if (IsBusy)
        {
            return;
        }

        if (!hasCompletedOperation)
        {
            StatusText = Text.Ready;
            CurrentStageText = Text.Ready;
            return;
        }

        StatusText = lastCompletedOperationSucceeded ? Text.OperationSucceeded : Text.OperationFailed;
        CurrentStageText = lastCompletedOperationSucceeded && lastCompletedOperationKind is { } operationKind
            ? CreateCompletedStageText(operationKind)
            : Text.OperationFailed;
    }

    private static string GetMediaKind(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".iso" => "ISO",
            ".cso" => "CSO",
            ".pkg" => "PKG",
            ".chd" => "CHD",
            ".cue" => "CUE",
            ".bin" => "BIN",
            ".gdi" => "GDI",
            "" => "Unknown",
            _ => "Other",
        };
    }

    private readonly record struct OperationOpenTarget(string Path, bool IsReport);

    private sealed record OperationRequest(
        UiOperationKind Kind,
        string InputPath,
        string OutputPath,
        CsoCompressionProfile Profile,
        uint BlockSize,
        int WorkerCount,
        bool ForceOverwrite,
        bool DeepVerify,
        bool ComputeSha256,
        bool CodecReport);
}
