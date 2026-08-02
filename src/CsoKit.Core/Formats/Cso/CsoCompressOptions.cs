namespace CsoKit.Core.Formats.Cso
{
    public sealed record CsoCompressOptions(
        string InputPath,
        string OutputPath,
        bool ForceOverwrite,
        uint BlockSize = CsoCompressor.DefaultBlockSize,
        IProgress<CsoCompressProgress>? Progress = null,
        CsoCompressionProfile Profile = CsoCompressionProfile.GameSafe,
        int WorkerCount = 1,
        bool UseZopfli = false,
        bool DeepVerifyOutput = false,
        bool CollectCodecReport = false,
        int CodecReportBlockLimit = 64,
        CancellationToken CancellationToken = default);
}