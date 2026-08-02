using System.IO.Compression;

namespace CsoKit.Core.Formats.Cso;

public static class CsoCompressionProfilePolicy
{
    public const string GameSafeName = "game-safe";
    public const string CompatName = "compat";
    public const string FastName = "fast";
    public const string SmallestName = "smallest";
    public const string ArchiveSmallestName = "archive-smallest";

    public static readonly string[] SupportedNames =
    [
        GameSafeName,
        CompatName,
        FastName,
        SmallestName,
        ArchiveSmallestName,
    ];

    public static string SupportedNamesText => string.Join("|", SupportedNames);

    public static CsoCompressionProfile DefaultProfile => CsoCompressionProfile.GameSafe;

    public static bool TryParse(string? value, out CsoCompressionProfile profile)
    {
        profile = DefaultProfile;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        switch (value.Trim().ToLowerInvariant())
        {
            case GameSafeName:
                profile = CsoCompressionProfile.GameSafe;
                return true;

            case CompatName:
                profile = CsoCompressionProfile.Compat;
                return true;

            case FastName:
                profile = CsoCompressionProfile.Fast;
                return true;

            case SmallestName:
                profile = CsoCompressionProfile.Smallest;
                return true;

            case ArchiveSmallestName:
                profile = CsoCompressionProfile.ArchiveSmallest;
                return true;

            default:
                return false;
        }
    }

    public static string GetCliName(CsoCompressionProfile profile)
    {
        return profile switch
        {
            CsoCompressionProfile.GameSafe => GameSafeName,
            CsoCompressionProfile.Compat => CompatName,
            CsoCompressionProfile.Fast => FastName,
            CsoCompressionProfile.Smallest => SmallestName,
            CsoCompressionProfile.ArchiveSmallest => ArchiveSmallestName,
            _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, "Unsupported CSO compression profile."),
        };
    }

    public static CsoCompressionProfileSettings Create(CsoCompressionProfile profile)
    {
        return profile switch
        {
            CsoCompressionProfile.GameSafe => new CsoCompressionProfileSettings(
                CsoCompressionProfile.GameSafe,
                GameSafeName,
                "game-safe",
                IsFast: false,
                Level: 9,
                CompressionLevel: CompressionLevel.SmallestSize),

            CsoCompressionProfile.Compat => new CsoCompressionProfileSettings(
                CsoCompressionProfile.Compat,
                CompatName,
                "compat",
                IsFast: false,
                Level: 9,
                CompressionLevel: CompressionLevel.SmallestSize),

            CsoCompressionProfile.Fast => new CsoCompressionProfileSettings(
                CsoCompressionProfile.Fast,
                FastName,
                "fast",
                IsFast: true,
                Level: 1,
                CompressionLevel: CompressionLevel.Fastest),

            CsoCompressionProfile.Smallest => new CsoCompressionProfileSettings(
                CsoCompressionProfile.Smallest,
                SmallestName,
                "smallest",
                IsFast: false,
                Level: 9,
                CompressionLevel: CompressionLevel.SmallestSize),

            CsoCompressionProfile.ArchiveSmallest => new CsoCompressionProfileSettings(
                CsoCompressionProfile.ArchiveSmallest,
                ArchiveSmallestName,
                "archive-smallest",
                IsFast: false,
                Level: 9,
                CompressionLevel: CompressionLevel.SmallestSize),

            _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, "Unsupported CSO compression profile."),
        };
    }
}
