namespace CsoKit.Core.Formats.Cso.Codecs;

public sealed record CsoCodecTrialResult(
    CsoCodecKind Kind,
    string Name,
    byte[] Buffer,
    int Length,
    bool Success,
    string? ErrorCode = null,
    string? ErrorMessage = null)
{
    public ReadOnlySpan<byte> OutputSpan => Buffer.AsSpan(0, Length);

    public static CsoCodecTrialResult Fail(
        CsoCodecKind kind,
        string name,
        string errorCode,
        string errorMessage)
    {
        return new CsoCodecTrialResult(kind, name, [], 0, Success: false, errorCode, errorMessage);
    }
}
