namespace CsoKit.Core.Formats.Cso;

public sealed record CsoDecompressOptions(
    string InputPath,
    string OutputPath,
    bool ForceOverwrite,
    IProgress<CsoDecompressProgress>? Progress = null,
    CancellationToken CancellationToken = default);