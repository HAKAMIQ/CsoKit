using CsoKit.Core.Native;

namespace CsoKit.Core.Formats.Cso.Codecs.Native;

public sealed class NativeZopfliTrial : ICsoCodecTrial
{
    private readonly int iterations;

    public NativeZopfliTrial(int iterations)
    {
        if (iterations is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(iterations), "Zopfli iterations must be between 1 and 100.");
        }

        this.iterations = iterations;
        Kind = iterations switch
        {
            <= 5 => CsoCodecKind.NativeZopfli5,
            <= 15 => CsoCodecKind.NativeZopfli15,
            _ => CsoCodecKind.NativeZopfli25,
        };
        Name = $"native-zopfli-{iterations}";
    }

    public CsoCodecKind Kind { get; }

    public string Name { get; }

    public bool TryCompressRawDeflate(
        ReadOnlySpan<byte> input,
        out CsoCodecTrialResult result)
    {
        NativeCsoCapabilities capabilities = NativeCsoRuntime.GetCapabilities();

        if (!capabilities.HasZopfli)
        {
            result = CsoCodecTrialResult.Fail(
                Kind,
                Name,
                "NativeCodecUnavailable",
                "Native Zopfli raw deflate is unavailable in this build.");
            return false;
        }

        if (!NativeCsoRuntime.TryDeflateZopfli(input, iterations, out byte[] compressed) ||
            compressed.Length == 0)
        {
            result = CsoCodecTrialResult.Fail(
                Kind,
                Name,
                "NativeCodecUnavailable",
                "Native Zopfli raw deflate failed for this block.");
            return false;
        }

        if (!RawDeflateVerifier.RoundtripEquals(compressed, input, input.Length))
        {
            result = CsoCodecTrialResult.Fail(
                Kind,
                Name,
                "NativeCodecRoundtripFailed",
                "Native Zopfli raw deflate failed roundtrip validation.");
            return false;
        }

        result = new CsoCodecTrialResult(Kind, Name, compressed, compressed.Length, Success: true);
        return true;
    }
}