using System.Globalization;

namespace CsoKit.Core.Formats.Cso;

public static class CsoFileNamePolicy
{
    public const int MinimumBaseNameLength = 2;
    public const int MaximumBaseNameLength = 10;

    private const string DefaultBaseName = "csokit";
    private static readonly HashSet<char> InvalidFileNameCharacters = [.. Path.GetInvalidFileNameChars()];

    public static bool TryValidateOutputPath(
        string outputPath,
        out string? errorCode,
        out string? errorMessage)
    {
        errorCode = null;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            errorCode = "InvalidOutputPath";
            errorMessage = "Output path is empty.";
            return false;
        }

        string baseName;

        try
        {
            baseName = Path.GetFileNameWithoutExtension(outputPath).Trim();
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            errorCode = "InvalidOutputPath";
            errorMessage = ex.Message;
            return false;
        }

        int length = CountTextElements(baseName);

        if (length < MinimumBaseNameLength)
        {
            errorCode = "OutputFileNameTooShort";
            errorMessage = $"Output file name must contain at least {MinimumBaseNameLength} characters, excluding the extension.";
            return false;
        }

        if (length > MaximumBaseNameLength)
        {
            errorCode = "OutputFileNameTooLong";
            errorMessage = $"Output file name must not exceed {MaximumBaseNameLength} characters, excluding the extension.";
            return false;
        }

        return true;
    }

    public static string NormalizeBaseName(string? baseName)
    {
        string normalized = Sanitize(baseName);

        if (CountTextElements(normalized) == 0)
        {
            normalized = DefaultBaseName;
        }

        normalized = TakeTextElements(normalized, MaximumBaseNameLength);

        while (CountTextElements(normalized) < MinimumBaseNameLength)
        {
            normalized += "_";
        }

        return normalized;
    }

    public static string CreateUniquePath(
        string directory,
        string baseName,
        string extension,
        string preferredSuffix = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentException.ThrowIfNullOrWhiteSpace(extension);

        string normalizedBaseName = NormalizeBaseName(baseName);
        string normalizedExtension = extension.StartsWith(".", StringComparison.Ordinal)
            ? extension
            : "." + extension;

        string preferredName = ComposeBaseName(normalizedBaseName, preferredSuffix);
        string preferredPath = Path.Combine(directory, preferredName + normalizedExtension);

        if (IsAvailable(preferredPath))
        {
            return preferredPath;
        }

        for (int number = 2; number < int.MaxValue; number++)
        {
            string suffix = string.IsNullOrEmpty(preferredSuffix)
                ? "-" + number.ToString(CultureInfo.InvariantCulture)
                : preferredSuffix + number.ToString(CultureInfo.InvariantCulture);

            if (CountTextElements(suffix) >= MaximumBaseNameLength)
            {
                break;
            }

            string candidateName = ComposeBaseName(normalizedBaseName, suffix);
            string candidatePath = Path.Combine(directory, candidateName + normalizedExtension);

            if (IsAvailable(candidatePath))
            {
                return candidatePath;
            }
        }

        throw new IOException("Could not create a unique output file name within the configured length limit.");
    }

    private static string ComposeBaseName(string normalizedBaseName, string suffix)
    {
        int suffixLength = CountTextElements(suffix);
        int availableBaseLength = MaximumBaseNameLength - suffixLength;

        if (availableBaseLength <= 0)
        {
            throw new ArgumentException("File-name suffix leaves no room for a base name.", nameof(suffix));
        }

        string truncatedBase = TakeTextElements(normalizedBaseName, availableBaseLength);
        string result = truncatedBase + suffix;

        while (CountTextElements(result) < MinimumBaseNameLength)
        {
            result = "_" + result;
        }

        return result;
    }

    private static string Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        char[] characters = value.Trim().Select(character =>
            InvalidFileNameCharacters.Contains(character) ? '_' : character).ToArray();

        return new string(characters).Trim().TrimEnd('.');
    }

    private static bool IsAvailable(string path)
    {
        return !File.Exists(path) && !Directory.Exists(path);
    }

    private static int CountTextElements(string value)
    {
        return StringInfo.ParseCombiningCharacters(value).Length;
    }

    private static string TakeTextElements(string value, int maximumLength)
    {
        if (maximumLength <= 0 || value.Length == 0)
        {
            return string.Empty;
        }

        int[] indexes = StringInfo.ParseCombiningCharacters(value);

        if (indexes.Length <= maximumLength)
        {
            return value;
        }

        return value[..indexes[maximumLength]];
    }
}
