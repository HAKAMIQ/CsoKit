using CsoKit.Core.Native;

namespace CsoKit.Core.Formats.Cso.Codecs.Native;

public sealed class NativeZlibTrial(
    CsoCodecKind kind,
    string name,
    NativeCsoRawCodec codec) : ICsoCodecTrial
{
    private readonly NativeCsoRawCodec rawCodec = codec;

    public CsoCodecKind Kind { get; } = kind;

    public string Name { get; } = name;

    public bool TryCompressRawDeflate(
        ReadOnlySpan<byte> input,
        out CsoCodecTrialResult result)
    {
        NativeCsoCapabilities capabilities = NativeCsoRuntime.GetCapabilities();

        if (!capabilities.HasZlib)
        {
            result = CsoCodecTrialResult.Fail(
                Kind,
                Name,
                "NativeCodecUnavailable",
                "Native zlib raw deflate is unavailable in this build.");
            return false;
        }

        if (!NativeCsoRuntime.TryDeflateRaw(rawCodec, level: 9, strategy: 0, input, out byte[] compressed) ||
            compressed.Length == 0)
        {
            result = CsoCodecTrialResult.Fail(
                Kind,
                Name,
                "NativeCodecUnavailable",
                "Native zlib raw deflate failed for this block.");
            return false;
        }

        if (!RawDeflateVerifier.RoundtripEquals(compressed, input, input.Length))
        {
            result = CsoCodecTrialResult.Fail(
                Kind,
                Name,
                "NativeCodecRoundtripFailed",
                "Native zlib raw deflate failed roundtrip validation.");
            return false;
        }

        result = new CsoCodecTrialResult(Kind, Name, compressed, compressed.Length, Success: true);
        return true;
    }
}