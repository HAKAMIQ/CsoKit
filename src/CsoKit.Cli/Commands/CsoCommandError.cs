namespace CsoKit.Cli.Commands;

public sealed record CsoCommandError(
    string Code,
    string Message);
