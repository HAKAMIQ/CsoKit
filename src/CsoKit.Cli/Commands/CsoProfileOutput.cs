using CsoKit.Core.Formats.Cso;

namespace CsoKit.Cli.Commands;

public sealed record CsoProfileOutput(
    string Name,
    bool Fast,
    int Level)
{
    public static CsoProfileOutput From(CsoCompressionProfileSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new CsoProfileOutput(
            settings.CliName,
            settings.IsFast,
            settings.Level);
    }
}
