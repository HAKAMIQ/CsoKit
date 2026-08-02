namespace CsoKit.Cli;

public static class CliExitCodes
{
    public const int Success = 0;
    public const int GeneralFailure = 1;
    public const int InvalidArguments = 2;
    public const int InputNotFound = 10;
    public const int InvalidCsoHeader = 11;
    public const int UnsupportedCsoVersion = 12;
    public const int CorruptIndexTable = 13;
    public const int OutputAlreadyExists = 14;
    public const int CannotWriteOutput = 15;
    public const int NotEnoughDiskSpace = 16;
    public const int DecompressionFailed = 20;
    public const int CompressionFailed = 21;
    public const int OperationCanceled = 130;
}