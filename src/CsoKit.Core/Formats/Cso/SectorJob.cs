namespace CsoKit.Core.Formats.Cso;

public sealed record SectorJob(
    int BlockIndex,
    ulong SourceOffset,
    int SourceLength,
    ReadOnlyMemory<byte> SourceBuffer)
{
    public ReadOnlySpan<byte> SourceSpan => SourceBuffer.Span[..SourceLength];
}
