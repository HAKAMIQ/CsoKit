namespace CsoKit.Core.Formats.Iso;

public sealed record PspIsoValidationIssue(
    string Code,
    string Message,
    string? Path = null);
