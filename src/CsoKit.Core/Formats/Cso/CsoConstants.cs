namespace CsoKit.Core.Formats.Cso;

public static class CsoConstants
{
    public const int MinimumHeaderSize = 24;
    public const int MaxSupportedBlockSize = 16 * 1024 * 1024;
    public const byte MaxSupportedIndexShift = 31;
    public const string MagicText = "CISO";

    public static readonly byte[] MagicBytes =
    [
        (byte)'C',
        (byte)'I',
        (byte)'S',
        (byte)'O'
    ];
}
