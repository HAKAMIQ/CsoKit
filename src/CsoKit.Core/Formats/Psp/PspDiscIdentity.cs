namespace CsoKit.Core.Formats.Psp;

public sealed record PspDiscIdentity(
    string? Title,
    string? DiscId,
    string? Category,
    string? PspSystemVersion)
{
    public static PspDiscIdentity Empty { get; } = new(null, null, null, null);
}
