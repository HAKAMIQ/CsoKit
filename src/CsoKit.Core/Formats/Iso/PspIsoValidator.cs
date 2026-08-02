using System.Text;
using CsoKit.Core.Formats.Psp;

namespace CsoKit.Core.Formats.Iso;

public static class PspIsoValidator
{
    private const int ParamSfoSafeReadLimit = 128 * 1024;
    private const int UmdDataSafeReadLimit = 4 * 1024;

    public static PspIsoValidationResult Validate(string inputPath, bool allowPadding = false)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            return PspIsoValidationResult.Fail(
                inputPath,
                inputBytes: 0,
                [new PspIsoValidationIssue("InvalidInputPath", "Input path is empty.")]);
        }

        if (!File.Exists(inputPath))
        {
            return PspIsoValidationResult.Fail(
                inputPath,
                inputBytes: 0,
                [new PspIsoValidationIssue("InputNotFound", "Input file was not found.")]);
        }

        try
        {
            FileInfo inputInfo = new(inputPath);
            IsoAlignmentResult alignment = IsoAlignmentPolicy.Validate(inputInfo.Length, allowPadding);
            List<PspIsoValidationIssue> issues = [];
            List<string> warnings = [];

            if (!alignment.Success)
            {
                issues.Add(new PspIsoValidationIssue(
                    alignment.ErrorCode ?? "IsoAlignmentFailed",
                    alignment.ErrorMessage ?? "ISO alignment validation failed."));
            }
            else if (alignment.PaddingBytes > 0)
            {
                warnings.Add($"ISO requires {alignment.PaddingBytes:N0} bytes of explicit zero padding to align to 2048 bytes.");
            }

            using FileStream input = new(
                inputPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 64 * 1024,
                FileOptions.SequentialScan);

            Iso9660Reader iso = new(input);
            bool hasPvd = iso.HasPrimaryVolumeDescriptor();

            if (!hasPvd)
            {
                issues.Add(new PspIsoValidationIssue(
                    "MissingIso9660PrimaryVolumeDescriptor",
                    "ISO9660 primary volume descriptor was not found."));
            }

            bool hasPspGame = false;
            bool hasUmdData = false;
            bool hasParamSfo = false;
            bool hasEboot = false;
            string? discIdFromUmdData = null;
            string? discIdFromParamSfo = null;
            string? title = null;
            string? category = null;
            string? pspSystemVersion = null;

            if (hasPvd)
            {
                hasPspGame = iso.TryFindPath("PSP_GAME", out Iso9660Entry pspGameEntry) && pspGameEntry.IsDirectory;
                hasUmdData = iso.TryFindPath("UMD_DATA.BIN", out Iso9660Entry umdDataEntry);
                hasParamSfo = iso.TryFindPath("PSP_GAME/PARAM.SFO", out Iso9660Entry paramSfoEntry);
                hasEboot = iso.TryFindPath("PSP_GAME/SYSDIR/EBOOT.BIN", out _);

                if (!hasPspGame)
                {
                    issues.Add(new PspIsoValidationIssue(
                        "MissingPspGameDirectory",
                        "Required PSP path PSP_GAME was not found.",
                        "PSP_GAME"));
                }

                if (!hasUmdData)
                {
                    issues.Add(new PspIsoValidationIssue(
                        "MissingUmdDataBin",
                        "Required PSP path UMD_DATA.BIN was not found.",
                        "UMD_DATA.BIN"));
                }
                else
                {
                    byte[] umdData = iso.ReadFile(umdDataEntry, UmdDataSafeReadLimit);
                    discIdFromUmdData = ExtractDiscIdFromText(umdData);

                    if (string.IsNullOrWhiteSpace(discIdFromUmdData))
                    {
                        warnings.Add("UMD_DATA.BIN was present, but a DISC_ID could not be extracted.");
                    }
                }

                if (!hasParamSfo)
                {
                    warnings.Add("PSP_GAME/PARAM.SFO was not found; title and PARAM.SFO DISC_ID are unavailable.");
                }
                else
                {
                    byte[] paramSfo = iso.ReadFile(paramSfoEntry, ParamSfoSafeReadLimit);

                    if (PspParamSfoReader.TryRead(paramSfo, out PspDiscIdentity identity, out string? warning))
                    {
                        title = identity.Title;
                        category = identity.Category;
                        pspSystemVersion = identity.PspSystemVersion;
                        discIdFromParamSfo = identity.DiscId;
                    }
                    else
                    {
                        warnings.Add(warning ?? "PARAM.SFO could not be parsed safely.");
                    }
                }

                if (!hasEboot)
                {
                    warnings.Add("PSP_GAME/SYSDIR/EBOOT.BIN was not found; boot payload presence could not be confirmed.");
                }

                if (!string.IsNullOrWhiteSpace(discIdFromUmdData) &&
                    !string.IsNullOrWhiteSpace(discIdFromParamSfo) &&
                    !string.Equals(NormalizeDiscId(discIdFromUmdData), NormalizeDiscId(discIdFromParamSfo), StringComparison.OrdinalIgnoreCase))
                {
                    warnings.Add($"DISC_ID mismatch: UMD_DATA.BIN reports {discIdFromUmdData}, PARAM.SFO reports {discIdFromParamSfo}.");
                }
            }
            else
            {
                issues.Add(new PspIsoValidationIssue(
                    "MissingPspGameDirectory",
                    "Required PSP path was not validated because ISO9660 could not be read.",
                    "PSP_GAME"));
                issues.Add(new PspIsoValidationIssue(
                    "MissingUmdDataBin",
                    "Required PSP path was not validated because ISO9660 could not be read.",
                    "UMD_DATA.BIN"));
            }

            return new PspIsoValidationResult(
                Success: issues.Count == 0,
                inputPath,
                inputInfo.Length,
                alignment.PaddingBytes,
                hasPvd,
                hasUmdData,
                hasParamSfo,
                hasEboot,
                issues,
                warnings,
                hasPspGame,
                discIdFromUmdData,
                discIdFromParamSfo,
                title,
                category,
                pspSystemVersion);
        }
        catch (UnauthorizedAccessException ex)
        {
            return PspIsoValidationResult.Fail(
                inputPath,
                inputBytes: 0,
                [new PspIsoValidationIssue("InputAccessDenied", ex.Message)]);
        }
        catch (IOException ex)
        {
            return PspIsoValidationResult.Fail(
                inputPath,
                inputBytes: 0,
                [new PspIsoValidationIssue("IsoReadFailed", ex.Message)]);
        }
        catch (InvalidDataException ex)
        {
            return PspIsoValidationResult.Fail(
                inputPath,
                inputBytes: 0,
                [new PspIsoValidationIssue("IsoStructureInvalid", ex.Message)]);
        }
    }

    private static string? ExtractDiscIdFromText(byte[] bytes)
    {
        string text = Encoding.ASCII.GetString(bytes).ToUpperInvariant();

        for (int index = 0; index <= text.Length - 10; index++)
        {
            if (IsAsciiLetter(text[index]) &&
                IsAsciiLetter(text[index + 1]) &&
                IsAsciiLetter(text[index + 2]) &&
                IsAsciiLetter(text[index + 3]) &&
                (text[index + 4] == '-' || text[index + 4] == '_') &&
                IsAsciiDigit(text[index + 5]) &&
                IsAsciiDigit(text[index + 6]) &&
                IsAsciiDigit(text[index + 7]) &&
                IsAsciiDigit(text[index + 8]) &&
                IsAsciiDigit(text[index + 9]))
            {
                return text.Substring(index, 10).Replace('_', '-');
            }
        }

        return null;
    }

    private static string NormalizeDiscId(string discId)
    {
        return discId.Trim().Replace('_', '-').ToUpperInvariant();
    }

    private static bool IsAsciiLetter(char value)
    {
        return value is >= 'A' and <= 'Z';
    }

    private static bool IsAsciiDigit(char value)
    {
        return value is >= '0' and <= '9';
    }
}