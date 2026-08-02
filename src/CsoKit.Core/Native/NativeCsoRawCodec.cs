namespace CsoKit.Core.Native;

public enum NativeCsoRawCodec
{
    ZlibDefault = 1,
    ZlibFiltered = 2,
    ZlibHuffmanOnly = 3,
    ZlibRle = 4,
    LibDeflate = 10,
    Zopfli = 20,
    SevenZipDeflate = 30,
}
