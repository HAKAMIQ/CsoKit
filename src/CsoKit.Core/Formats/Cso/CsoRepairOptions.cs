namespace CsoKit.Core.Formats.Cso;

public sealed record CsoRepairOptions(
    string InputPath,
    string OutputPath,
    bool ForceOverwrite,
    CsoCompressionProfile Profile = CsoCompressionProfile.GameSafe,
    bool PadLastSector = false,
    bool DeepVerify = true,
    bool CollectCodecReport = false,
    int CodecReportBlockLimit = 64,
    IProgress<CsoCompressProgress>? Progress = null,
    CancellationToken CancellationToken = default);