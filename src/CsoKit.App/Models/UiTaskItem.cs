using System.IO;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CsoKit.App.Models;

public sealed class UiTaskItem(string path, string displayName, string mediaKind, string userFacingStatus) : INotifyPropertyChanged
{
    private string userFacingStatus = userFacingStatus;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Path { get; } = path;

    public string DisplayName { get; } = displayName;

    public string MediaKind { get; } = mediaKind;

    public string UserFacingStatus
    {
        get => userFacingStatus;
        private set
        {
            if (userFacingStatus == value)
            {
                return;
            }

            userFacingStatus = value;
            OnPropertyChanged();
        }
    }

    public static UiTaskItem Create(string path, string userFacingStatus)
    {
        string displayName = Directory.Exists(path)
            ? new DirectoryInfo(path).Name
            : System.IO.Path.GetFileNameWithoutExtension(path);

        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = System.IO.Path.GetFileName(path);
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = path;
        }

        string mediaKind = Directory.Exists(path)
            ? "Folder"
            : GetMediaKind(System.IO.Path.GetExtension(path));

        return new UiTaskItem(path, displayName, mediaKind, userFacingStatus);
    }

    public void SetStatus(string userFacingStatus)
    {
        UserFacingStatus = userFacingStatus;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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
}
