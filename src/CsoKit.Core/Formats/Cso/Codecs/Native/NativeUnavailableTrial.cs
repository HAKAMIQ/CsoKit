namespace CsoKit.Core.Formats.Cso.Codecs.Native;

public sealed class NativeUnavailableTrial(
    CsoCodecKind kind,
    string name,
    string errorCode,
    string errorMessage) : ICsoCodecTrial
{
    private readonly string failureCode = string.IsNullOrWhiteSpace(errorCode)
        ? "NativeCodecUnavailable"
        : errorCode;

    private readonly string failureMessage = string.IsNullOrWhiteSpace(errorMessage)
        ? "Native codec is unavailable in this build."
        : errorMessage;

    public CsoCodecKind Kind { get; } = kind;

    public string Name { get; } = string.IsNullOrWhiteSpace(name)
        ? kind.ToString()
        : name;

    public bool TryCompressRawDeflate(
        ReadOnlySpan<byte> _,
        out CsoCodecTrialResult result)
    {
        result = CsoCodecTrialResult.Fail(Kind, Name, failureCode, failureMessage);
        return false;
    }
}