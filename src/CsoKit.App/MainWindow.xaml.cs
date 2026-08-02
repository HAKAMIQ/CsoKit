using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using CsoKit.App.Models;
using CsoKit.App.ViewModels;
using Microsoft.Win32;

namespace CsoKit.App;

public partial class MainWindow : Window
{
    private const int DwmUseImmersiveDarkMode = 20;
    private const int DwmUseImmersiveDarkModeBefore20H1 = 19;

    private readonly MainWindowViewModel viewModel = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ApplyNativeDarkTitleBar();
    }

    private async void StartOperationButton_Click(object sender, RoutedEventArgs e)
    {
        await viewModel.RunSelectedOperationAsync().ConfigureAwait(true);
    }

    private void StopOperationButton_Click(object sender, RoutedEventArgs e)
    {
        viewModel.CancelCurrentOperation();
    }

    private void ClearLogButton_Click(object sender, RoutedEventArgs e)
    {
        viewModel.ClearLog();
    }

    private void LanguageToggleButton_Click(object sender, RoutedEventArgs e)
    {
        viewModel.ToggleLanguage();
    }

    private void GearButton_Click(object sender, RoutedEventArgs e)
    {
        viewModel.ToggleAdvancedOptions();
    }

    private void BrowseInputButton_Click(object sender, RoutedEventArgs e)
    {
        ShowInputFilesDialog();
    }

    private void ShowInputFilesDialog()
    {
        if (!viewModel.CanEdit)
        {
            return;
        }

        OpenFileDialog dialog = new()
        {
            Title = viewModel.Text.SelectInputFile,
            Filter = viewModel.Text.InputFileDialogFilter,
            CheckFileExists = true,
            Multiselect = true,
        };

        if (dialog.ShowDialog(this) == true)
        {
            viewModel.SetInputPathsFromUser(dialog.FileNames);
        }
    }

    private void TaskListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        ShowInputFilesDialog();
        e.Handled = true;
    }

    private void BrowseFolderButton_Click(object sender, RoutedEventArgs e)
    {
        OpenFolderDialog dialog = new()
        {
            Title = viewModel.Text.BrowseFolder,
            Multiselect = false,
        };

        if (dialog.ShowDialog(this) == true)
        {
            viewModel.SetInputPathFromUser(dialog.FolderName);
        }
    }

    private void BrowseOutputButton_Click(object sender, RoutedEventArgs e)
    {
        SaveFileDialog dialog = new()
        {
            Title = viewModel.Text.SelectOutputFile,
            Filter = viewModel.Text.OutputFileDialogFilter,
            OverwritePrompt = false,
            AddExtension = true,
            FileName = string.IsNullOrWhiteSpace(viewModel.OutputPath)
                ? viewModel.GetSuggestedOutputPath()
                : viewModel.OutputPath,
        };

        if (dialog.ShowDialog(this) == true)
        {
            viewModel.SetOutputPathFromUser(dialog.FileName);
        }
    }

    private void OperationRadioButton_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton radioButton || radioButton.Tag is null)
        {
            return;
        }

        string? operationName = radioButton.Tag.ToString();

        if (Enum.TryParse(operationName, ignoreCase: false, out UiOperationKind operationKind))
        {
            viewModel.SelectedOperation = operationKind;
        }
    }

    private void OpenOutputButton_Click(object sender, RoutedEventArgs e)
    {
        OpenExplorerTarget(viewModel.OpenTargetPath);
    }

    private static void OpenExplorerTarget(string targetPath)
    {
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            return;
        }

        if (File.Exists(targetPath))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{targetPath}\"",
                UseShellExecute = false,
            });

            return;
        }

        if (Directory.Exists(targetPath))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = targetPath,
                UseShellExecute = true,
            });
        }
    }

    private void InputDropZone_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = viewModel.CanEdit && e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;

        e.Handled = true;
    }

    private void InputDropZone_Drop(object sender, DragEventArgs e)
    {
        if (!viewModel.CanEdit ||
            e.Data.GetData(DataFormats.FileDrop) is not string[] files ||
            files.Length == 0)
        {
            e.Handled = true;
            return;
        }

        viewModel.SetInputPathsFromUser(files);
        e.Handled = true;
    }

    private void ApplyNativeDarkTitleBar()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
        {
            return;
        }

        IntPtr handle = new WindowInteropHelper(this).Handle;

        if (handle == IntPtr.Zero)
        {
            return;
        }

        int enabled = 1;
        int size = Marshal.SizeOf<int>();

        try
        {
            int result = DwmSetWindowAttribute(handle, DwmUseImmersiveDarkMode, ref enabled, size);

            if (result != 0)
            {
                DwmSetWindowAttribute(handle, DwmUseImmersiveDarkModeBefore20H1, ref enabled, size);
            }
        }
        catch (DllNotFoundException)
        {
        }
        catch (EntryPointNotFoundException)
        {
        }
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int attributeValue, int attributeSize);

}
