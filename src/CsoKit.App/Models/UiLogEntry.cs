using System;
using System.Globalization;

namespace CsoKit.App.Models;

public sealed record UiLogEntry(
    string Title,
    string Message,
    string Kind)
{
    public DateTimeOffset CreatedAt { get; } = DateTimeOffset.Now;

    public string TimeText => CreatedAt.ToString("HH:mm:ss", CultureInfo.CurrentCulture);
}
