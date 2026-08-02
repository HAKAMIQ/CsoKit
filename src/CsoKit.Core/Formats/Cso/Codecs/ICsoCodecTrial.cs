namespace CsoKit.Core.Formats.Cso.Codecs;

public interface ICsoCodecTrial
{
    CsoCodecKind Kind { get; }

    string Name { get; }

    bool TryCompressRawDeflate(
        ReadOnlySpan<byte> input,
        out CsoCodecTrialResult result);
}
