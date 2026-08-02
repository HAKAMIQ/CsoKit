namespace CsoKit.Core.Formats.Iso;

public sealed record Iso9660Entry(
    string Name,
    uint Extent,
    uint Size,
    bool IsDirectory);
