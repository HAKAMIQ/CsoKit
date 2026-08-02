namespace CsoKit.Core.Formats.Cso.Codecs;

public enum CsoCodecKind
{
    Store = 0,

    ManagedDeflateFastest = 10,
    ManagedDeflateOptimal = 11,
    ManagedDeflateSmallest = 12,

    NativeZlibDefault = 20,
    NativeZlibFiltered = 21,
    NativeZlibHuffmanOnly = 22,
    NativeZlibRle = 23,

    NativeLibDeflate1 = 30,
    NativeLibDeflate6 = 31,
    NativeLibDeflate9 = 32,
    NativeLibDeflate12 = 33,

    NativeZopfli5 = 40,
    NativeZopfli15 = 41,
    NativeZopfli25 = 42,

    NativeSevenZipDeflate = 50,
}
