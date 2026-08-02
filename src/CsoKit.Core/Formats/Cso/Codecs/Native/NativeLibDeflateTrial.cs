using CsoKit.Core.Native;

namespace CsoKit.Core.Formats.Cso.Codecs.Native;

public sealed class NativeLibDeflateTrial(
    CsoCodecKind kind,
    string name,
    int level) : ICsoCodecTrial
{
    private readonly int rawLevel = ValidateLevel(level);

    public CsoCodecKind Kind { get; } = kind;

    public string Name { get; } = name;

    public bool TryCompressRawDeflate(
        ReadOnlySpan<byte> input,
        out CsoCodecTrialResult result)
    {
        NativeCsoCapabilities capabilities = NativeCsoRuntime.GetCapabilities();

        if (!capabilities.HasLibDeflate)
        {
            result = CsoCodecTrialResult.Fail(
                Kind,
                Name,
                "NativeCodecUnavailable",
                "Native libdeflate raw deflate is unavailable in this build.");
            return false;
        }

        if (!NativeCsoRuntime.TryDeflateRaw(NativeCsoRawCodec.LibDeflate, rawLevel, strategy: 0, input, out byte[] compressed) ||
            compressed.Length == 0)
        {
            result = CsoCodecTrialResult.Fail(
                Kind,
                Name,
                "NativeCodecUnavailable",
                "Native libdeflate raw deflate failed for this block.");
            return false;
        }

        if (!RawDeflateVerifier.RoundtripEquals(compressed, input, input.Length))
        {
            result = CsoCodecTrialResult.Fail(
                Kind,
                Name,
                "NativeCodecRoundtripFailed",
                "Native libdeflate raw deflate failed roundtrip validation.");
            return false;
        }

        result = new CsoCodecTrialResult(Kind, Name, compressed, compressed.Length, Success: true);
        return true;
    }

    private static int ValidateLevel(int level)
    {
        if (level is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(level), "libdeflate level must be between 1 and 12.");
        }

        return level;
    }
}