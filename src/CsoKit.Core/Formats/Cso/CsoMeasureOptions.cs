namespace CsoKit.Core.Formats.Cso;

public sealed record CsoMeasureOptions(
    string InputPath,
    uint BlockSize = CsoCompressor.DefaultBlockSize,
    IProgress<CsoCompressProgress>? Progress = null,
    CsoCompressionProfile Profile = CsoCompressionProfile.GameSafe,
    bool UseZopfli = false,
    CancellationToken CancellationToken = default);