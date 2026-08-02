namespace CsoKit.Core.Native;

public sealed record NativeCsoCapabilities(
    uint AbiVersion,
    bool HasZlib,
    bool HasLibDeflate,
    bool HasZopfli,
    bool HasSevenZipDeflate,
    bool HasLz4)
{
    public static NativeCsoCapabilities ManagedFallback { get; } = new(
        AbiVersion: 0,
        HasZlib: false,
        HasLibDeflate: false,
        HasZopfli: false,
        HasSevenZipDeflate: false,
        HasLz4: false);
}
