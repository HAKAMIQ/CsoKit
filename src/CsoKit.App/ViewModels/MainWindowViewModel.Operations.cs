using System.Globalization;
using System.IO;
using CsoKit.App.Localization;
using CsoKit.App.Models;
using CsoKit.Application;
using CsoKit.Core.Compression;
using CsoKit.Core.Formats.Cso;

namespace CsoKit.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    public void CancelCurrentOperation()
    {
        CancellationTokenSource? cancellation = operationCancellation;

        if (cancellation is null || cancellation.IsCancellationRequested)
        {
            return;
        }

        cancellation.Cancel();
        CurrentStageText = Text.StopOperation;
        SelectedTask?.SetStatus(CurrentStageText);
        OnPropertyChanged(nameof(CanCancel));
    }

    public async Task RunSelectedOperationAsync()
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            OperationRequest request = CreateRequest();
            UiTaskItem? activeTask = SelectedTask;
            string operationName = GetOperationName(request.Kind);

            IsBusy = true;
            ClearOpenTarget();
            StatusText = $"{Text.Running}: {operationName}";
            CurrentStageText = CreatePreparingStageText(request.Kind);
            activeTask?.SetStatus(CurrentStageText);
            ProgressValue = 0;
            IsProgressIndeterminate = true;

            AddLog(Text.OperationStarted, BuildOperationStartMessage(request, operationName), "Info");

            Progress<double> progress = new(value =>
            {
                IsProgressIndeterminate = false;
                ProgressValue = Math.Clamp(value, 0, 100);
                CurrentStageText = CreateProgressStageText(request.Kind, ProgressValue);
                activeTask?.SetStatus(CurrentStageText);
            });

            using CancellationTokenSource cancellation = new();
            operationCancellation = cancellation;
            OnPropertyChanged(nameof(CanCancel));

            CsoOperationResult result = await Task.Run(
                () => ExecuteRequest(request, progress, cancellation.Token),
                cancellation.Token).ConfigureAwait(true);

            if (cancellation.IsCancellationRequested ||
                string.Equals(result.ErrorCode, "OperationCanceled", StringComparison.Ordinal))
            {
                throw new OperationCanceledException(cancellation.Token);
            }

            IsProgressIndeterminate = false;
            ProgressValue = result.Success ? 100 : 0;
            hasCompletedOperation = true;
            lastCompletedOperationKind = request.Kind;
            lastCompletedOperationSucceeded = result.Success;
            StatusText = result.Success ? Text.OperationSucceeded : Text.OperationFailed;
            CurrentStageText = result.Success ? CreateCompletedStageText(request.Kind) : Text.OperationFailed;
            activeTask?.SetStatus(CurrentStageText);
            string? reportPath = TryWriteOperationReport(request, result, operationName);
            OperationOpenTarget? openTarget = ResolveOpenTarget(request, reportPath);

            if (openTarget is { } target)
            {
                SetOpenTarget(target.Path, target.IsReport);
            }
            else
            {
                ClearOpenTarget();
            }

            Summary = CreateSummary(result, operationName);

            AddLog(
                result.Success ? Text.OperationSucceeded : Text.OperationFailed,
                operationName,
                result.Success ? "Success" : "Error");

            if (!string.IsNullOrWhiteSpace(result.Details))
            {
                AddLog(Text.TechnicalDetails, SimplifyDetailsForUser(result.Details), "Info");
            }
        }
        catch (OperationCanceledException)
        {
            IsProgressIndeterminate = false;
            ProgressValue = 0;
            hasCompletedOperation = true;
            lastCompletedOperationKind = SelectedOperation;
            lastCompletedOperationSucceeded = false;
            StatusText = Text.OperationFailed;
            CurrentStageText = Text.StopOperation;
            SelectedTask?.SetStatus(CurrentStageText);
            Summary = new OperationSummaryViewModel(
                Text.OperationFailed,
                Text.StopOperation,
                "-",
                "-",
                "-");
            AddLog(Text.StopOperation, Text.StopOperation, "Info");
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            IsProgressIndeterminate = false;
            ProgressValue = 0;
            hasCompletedOperation = true;
            lastCompletedOperationKind = SelectedOperation;
            lastCompletedOperationSucceeded = false;
            StatusText = Text.OperationFailed;
            CurrentStageText = Text.OperationFailed;
            SelectedTask?.SetStatus(CurrentStageText);
            Summary = new OperationSummaryViewModel(
                Text.OperationFailed,
                ex.Message,
                "-",
                "-",
                "-");

            AddLog(Text.Error, ex.Message, "Error");
        }
        finally
        {
            operationCancellation?.Dispose();
            operationCancellation = null;
            IsBusy = false;
            OnPropertyChanged(nameof(CanCancel));
        }
    }

    private OperationRequest CreateRequest()
    {
        if (string.IsNullOrWhiteSpace(InputPath))
        {
            throw new InvalidOperationException(Text.InputPathRequired);
        }

        CsoCompressionProfile profile = SelectedOperationUsesProfile
            ? GetSelectedProfile()
            : CsoCompressionProfile.GameSafe;
        uint blockSize = SelectedOperationUsesBlockSize
            ? GetBlockSize()
            : CsoCompressor.DefaultBlockSize;
        int workerCount = SelectedOperationUsesWorkerCount
            ? GetWorkerCount()
            : 1;
        string effectiveOutputPath = RequiresOutputPath
            ? GetOrCreateOutputPath()
            : string.Empty;

        return new OperationRequest(
            SelectedOperation,
            InputPath,
            effectiveOutputPath,
            profile,
            blockSize,
            workerCount,
            ForceOverwrite,
            SelectedOperationUsesDeepVerify && DeepVerify,
            SelectedOperation is UiOperationKind.Verify && ComputeSha256,
            SelectedOperationUsesCodecReport && CollectCodecReport);
    }

    private string GetOrCreateOutputPath()
    {
        string outputPath;

        if (!string.IsNullOrWhiteSpace(OutputPath))
        {
            outputPath = OutputPath;
        }
        else
        {
            outputPath = GetSuggestedOutputPath();

            if (string.IsNullOrWhiteSpace(outputPath))
            {
                throw new InvalidOperationException(Text.OperationDoesNotUseOutput);
            }

            SetOutputPathInternally(outputPath);
        }

        if (!CsoOperationService.TryValidateOutputFileName(outputPath, out _))
        {
            throw new InvalidOperationException(Text.InvalidOutputFileNameLength);
        }

        return outputPath;
    }

    private CsoCompressionProfile GetSelectedProfile()
    {
        if (!CsoCompressionProfilePolicy.TryParse(SelectedProfileName, out CsoCompressionProfile profile))
        {
            throw new InvalidOperationException(Text.InvalidCompressionProfile);
        }

        return profile;
    }

    private uint GetBlockSize()
    {
        if (!uint.TryParse(BlockSizeText, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint blockSize) || blockSize == 0)
        {
            throw new InvalidOperationException(Text.InvalidBlockSize);
        }

        return blockSize;
    }

    private int GetWorkerCount()
    {
        if (!int.TryParse(
                WorkerCountText,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int workerCount))
        {
            throw new InvalidOperationException(Text.InvalidThreads);
        }

        if (!CsoWorkerPolicy.TryValidate(workerCount, out string? workerError))
        {
            throw new InvalidOperationException(workerError ?? Text.InvalidThreads);
        }

        return workerCount;
    }

    private void RefreshSuggestedOutputPath()
    {
        if (outputPathWasEditedByUser || string.IsNullOrWhiteSpace(InputPath))
        {
            return;
        }

        string suggested = CreateSuggestedOutputPath(InputPath, SelectedOperation);
        SetOutputPathInternally(suggested);
        OnPropertyChanged(nameof(CompactOutputText));
        OnPropertyChanged(nameof(CanOpenOutput));
    }

    private void SetOutputPathInternally(string value)
    {
        isSettingOutputPathInternally = true;

        try
        {
            OutputPath = value;
        }
        finally
        {
            isSettingOutputPathInternally = false;
        }
    }

    private static string CreateSuggestedOutputPath(string inputPath, UiOperationKind operationKind)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            return string.Empty;
        }

        try
        {
            return operationKind switch
            {
                UiOperationKind.Compress => CsoOperationService.CreateSuggestedCompressOutputPath(inputPath),
                UiOperationKind.Decompress => CsoOperationService.CreateSuggestedDecompressOutputPath(inputPath),
                UiOperationKind.Repair => CsoOperationService.CreateSuggestedRepairOutputPath(inputPath),
                _ => string.Empty,
            };
        }
        catch (ArgumentException)
        {
            return string.Empty;
        }
        catch (NotSupportedException)
        {
            return string.Empty;
        }
        catch (PathTooLongException)
        {
            return string.Empty;
        }
        catch (IOException)
        {
            return string.Empty;
        }
    }

    private static CsoOperationResult ExecuteRequest(
        OperationRequest request,
        IProgress<double> progress,
        CancellationToken cancellationToken)
    {
        return request.Kind switch
        {
            UiOperationKind.Detect => CsoOperationService.Detect(request.InputPath, cancellationToken),
            UiOperationKind.Analyze => CsoOperationService.Analyze(request.InputPath, cancellationToken),
            UiOperationKind.Measure => CsoOperationService.Measure(
                request.InputPath,
                request.Profile,
                request.BlockSize,
                progress,
                cancellationToken),
            UiOperationKind.Compress => CsoOperationService.Compress(
                request.InputPath,
                request.OutputPath,
                request.Profile,
                request.BlockSize,
                request.WorkerCount,
                request.ForceOverwrite,
                request.DeepVerify,
                request.CodecReport,
                progress,
                cancellationToken),
            UiOperationKind.Decompress => CsoOperationService.Decompress(
                request.InputPath,
                request.OutputPath,
                request.ForceOverwrite,
                progress,
                cancellationToken),
            UiOperationKind.Verify => CsoOperationService.Verify(
                request.InputPath,
                request.DeepVerify,
                request.ComputeSha256,
                progress,
                cancellationToken),
            UiOperationKind.Repair => CsoOperationService.Repair(
                request.InputPath,
                request.OutputPath,
                request.Profile,
                request.ForceOverwrite,
                request.DeepVerify,
                request.CodecReport,
                progress,
                cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(request), request.Kind, ArabicUiText.English.UnsupportedOperation),
        };
    }

    private OperationSummaryViewModel CreateSummary(CsoOperationResult result, string operationName)
    {
        string originalSize = FormatOptionalBytes(result.OriginalBytes);
        string resultSize = FormatOptionalBytes(result.ResultBytes);
        string savedSize = TryCreateSavingsText(result.OriginalBytes, result.ResultBytes);

        return new OperationSummaryViewModel(
            result.Success ? Text.OperationSucceeded : Text.OperationFailed,
            operationName,
            originalSize,
            resultSize,
            savedSize);
    }

    private static string FormatOptionalBytes(long? value)
    {
        return value is null || value < 0
            ? "-"
            : value.Value.ToString("N0", CultureInfo.CurrentCulture);
    }

    private static string TryCreateSavingsText(long? originalSize, long? resultSize)
    {
        if (originalSize is null || resultSize is null || originalSize <= 0 || resultSize < 0)
        {
            return "-";
        }

        long saved = originalSize.Value - resultSize.Value;
        double savedPercent = saved / (double)originalSize.Value;

        return string.Format(
            CultureInfo.CurrentCulture,
            "{0:N0} ({1:P2})",
            saved,
            savedPercent);
    }

}
