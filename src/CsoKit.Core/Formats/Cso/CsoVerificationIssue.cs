namespace CsoKit.Core.Formats.Cso;

public sealed record CsoVerificationIssue(
    string Code,
    string Message,
    int? BlockIndex = null)
{
    public long? Offset { get; init; }

    public string? Expected { get; init; }

    public string? Actual { get; init; }
}