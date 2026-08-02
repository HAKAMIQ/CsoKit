namespace CsoKit.Core.Formats.Cso;

public sealed class CsoDiskSpacePreflight
{
    public CsoDiskSpacePreflightResult CheckOutputSpace(
        string outputPath,
        ulong requiredBytes)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return CsoDiskSpacePreflightResult.Fail(
                "InvalidOutputPath",
                "Output path is empty.",
                requiredBytes);
        }

        string fullOutputPath;

        try
        {
            fullOutputPath = Path.GetFullPath(outputPath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return CsoDiskSpacePreflightResult.Fail(
                "InvalidOutputPath",
                ex.Message,
                requiredBytes);
        }

        string? rootPath = Path.GetPathRoot(fullOutputPath);

        if (string.IsNullOrWhiteSpace(rootPath))
        {
            return CsoDiskSpacePreflightResult.Fail(
                "OutputDriveNotFound",
                "Could not determine output drive.",
                requiredBytes);
        }

        try
        {
            DriveInfo drive = new(rootPath);

            if (!drive.IsReady)
            {
                return CsoDiskSpacePreflightResult.Fail(
                    "OutputDriveNotReady",
                    "Output drive is not ready.",
                    requiredBytes);
            }

            ulong availableBytes = drive.AvailableFreeSpace <= 0
                ? 0
                : checked((ulong)drive.AvailableFreeSpace);

            if (availableBytes < requiredBytes)
            {
                return CsoDiskSpacePreflightResult.Fail(
                    "NotEnoughDiskSpace",
                    $"Not enough free space on the output drive. Required: {requiredBytes:N0} bytes, available: {availableBytes:N0} bytes.",
                    requiredBytes,
                    availableBytes);
            }

            return CsoDiskSpacePreflightResult.Ok(requiredBytes, availableBytes);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return CsoDiskSpacePreflightResult.Fail(
                "OutputDriveCheckFailed",
                ex.Message,
                requiredBytes);
        }
    }
}
