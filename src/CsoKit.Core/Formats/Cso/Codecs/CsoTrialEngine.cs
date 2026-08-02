using System.Diagnostics;
using CsoKit.Core.Compression.Trials;

namespace CsoKit.Core.Formats.Cso.Codecs;

public sealed class CsoTrialEngine(
    IReadOnlyList<ICsoCodecTrial> trials,
    CsoBestCandidateSelector selector)
{
    private readonly IReadOnlyList<ICsoCodecTrial> codecTrials = trials ?? throw new ArgumentNullException(nameof(trials));
    private readonly CsoBestCandidateSelector candidateSelector = selector ?? throw new ArgumentNullException(nameof(selector));

    public SectorResult Compress(SectorJob job)
    {
        List<SectorResult> candidates = new(codecTrials.Count);

        foreach (ICsoCodecTrial trial in codecTrials)
        {
            if (!trial.TryCompressRawDeflate(job.SourceSpan, out CsoCodecTrialResult result) ||
                !result.Success)
            {
                continue;
            }

            if (result.Length <= 0 || result.Length >= job.SourceLength)
            {
                continue;
            }

            if (!RawDeflateVerifier.RoundtripEquals(
                result.OutputSpan,
                job.SourceSpan,
                expectedBytes: job.SourceLength))
            {
                continue;
            }

            candidates.Add(ToSectorResult(job, result));
        }

        return candidateSelector.Select(job, candidates);
    }

    public SectorResult CompressWithReport(SectorJob job)
    {
        List<SectorResult> candidates = new(codecTrials.Count);
        List<CodecTrialCandidateResult> candidateReports = new(codecTrials.Count + 1);

        foreach (ICsoCodecTrial trial in codecTrials)
        {
            Stopwatch encodeTimer = Stopwatch.StartNew();
            bool produced = trial.TryCompressRawDeflate(job.SourceSpan, out CsoCodecTrialResult result);
            encodeTimer.Stop();

            if (!produced || !result.Success)
            {
                candidateReports.Add(CreateCandidateReport(
                    trial.Kind,
                    trial.Name,
                    compressedBytes: 0,
                    sourceBytes: job.SourceLength,
                    encodeMilliseconds: encodeTimer.Elapsed.TotalMilliseconds,
                    decodeMilliseconds: 0,
                    passedRoundtrip: false,
                    rejectedReason: result.ErrorCode ?? "CodecUnavailable",
                    selectedWinner: false,
                    fallbackReason: result.ErrorMessage));
                continue;
            }

            if (result.Length <= 0)
            {
                candidateReports.Add(CreateCandidateReport(
                    result.Kind,
                    result.Name,
                    result.Length,
                    job.SourceLength,
                    encodeTimer.Elapsed.TotalMilliseconds,
                    decodeMilliseconds: 0,
                    passedRoundtrip: false,
                    rejectedReason: "InvalidCompressedBytes",
                    selectedWinner: false,
                    fallbackReason: null));
                continue;
            }

            if (result.Length >= job.SourceLength)
            {
                candidateReports.Add(CreateCandidateReport(
                    result.Kind,
                    result.Name,
                    result.Length,
                    job.SourceLength,
                    encodeTimer.Elapsed.TotalMilliseconds,
                    decodeMilliseconds: 0,
                    passedRoundtrip: false,
                    rejectedReason: "NotSmallerThanStored",
                    selectedWinner: false,
                    fallbackReason: "Stored block is smaller or equal."));
                continue;
            }

            Stopwatch decodeTimer = Stopwatch.StartNew();
            bool passedRoundtrip = RawDeflateVerifier.RoundtripEquals(
                result.OutputSpan,
                job.SourceSpan,
                expectedBytes: job.SourceLength);
            decodeTimer.Stop();

            if (!passedRoundtrip)
            {
                candidateReports.Add(CreateCandidateReport(
                    result.Kind,
                    result.Name,
                    result.Length,
                    job.SourceLength,
                    encodeTimer.Elapsed.TotalMilliseconds,
                    decodeTimer.Elapsed.TotalMilliseconds,
                    passedRoundtrip: false,
                    rejectedReason: "RoundtripMismatch",
                    selectedWinner: false,
                    fallbackReason: "Codec output did not decode back to the source block."));
                continue;
            }

            candidates.Add(ToSectorResult(
                job,
                result,
                encodeTimer.Elapsed.TotalMilliseconds,
                decodeTimer.Elapsed.TotalMilliseconds,
                passedRoundtrip));

            candidateReports.Add(CreateCandidateReport(
                result.Kind,
                result.Name,
                result.Length,
                job.SourceLength,
                encodeTimer.Elapsed.TotalMilliseconds,
                decodeTimer.Elapsed.TotalMilliseconds,
                passedRoundtrip: true,
                rejectedReason: null,
                selectedWinner: false,
                fallbackReason: null));
        }

        SectorResult selected = candidateSelector.Select(job, candidates);
        bool storedFallback = selected.IsStored;
        string selectedCodec = selected.EffectiveCodecName;

        if (storedFallback)
        {
            candidateReports.Add(new CodecTrialCandidateResult(
                "store",
                "store",
                0,
                job.SourceLength,
                1.0,
                0,
                0,
                PassedRoundtrip: true,
                RejectedReason: null,
                SelectedWinner: true,
                FallbackReason: candidates.Count == 0 ? "No candidate passed and beat stored bytes." : "Stored bytes won size policy."));
        }

        CodecTrialCandidateResult[] finalized = new CodecTrialCandidateResult[candidateReports.Count];

        for (int index = 0; index < finalized.Length; index++)
        {
            finalized[index] = FinalizeCandidateReport(candidateReports[index], selected, selectedCodec);
        }

        CodecTrialReport report = new(
            job.BlockIndex,
            job.SourceLength,
            finalized,
            selectedCodec,
            storedFallback);

        return selected with { TrialReport = report };
    }

    private static SectorResult ToSectorResult(
        SectorJob job,
        CsoCodecTrialResult result,
        double encodeMilliseconds = 0,
        double decodeMilliseconds = 0,
        bool passedRoundtrip = true)
    {
        return new SectorResult(
            job.BlockIndex,
            job.SourceOffset,
            job.SourceLength,
            result.Length,
            IsStored: false,
            Method: result.Kind is CsoCodecKind.NativeZopfli5 or CsoCodecKind.NativeZopfli15 or CsoCodecKind.NativeZopfli25
                ? CompressionMethod.ZopfliDeflate
                : CompressionMethod.RawDeflate,
            Level: GetLogicalLevel(result.Kind),
            Buffer: result.Buffer,
            CodecName: result.Name,
            DecisionMetrics: CreateDecisionMetrics(
                result.Kind,
                result.Length,
                job.SourceLength,
                encodeMilliseconds,
                decodeMilliseconds,
                passedRoundtrip));
    }

    private static CodecTrialCandidateResult FinalizeCandidateReport(
        CodecTrialCandidateResult candidate,
        SectorResult selected,
        string selectedCodec)
    {
        if (candidate.CodecName.Equals(selectedCodec, StringComparison.OrdinalIgnoreCase))
        {
            return candidate with { SelectedWinner = true };
        }

        if (!string.IsNullOrWhiteSpace(candidate.RejectedReason))
        {
            return candidate;
        }

        string rejectedReason = selected.IsStored
            ? "StoredPolicyWinner"
            : candidate.CompressedBytes <= selected.OutputLength
                ? "HigherDecisionCostOrPolicy"
                : "LargerThanSelected";

        return candidate with { RejectedReason = rejectedReason };
    }

    private static CodecTrialDecisionMetrics CreateDecisionMetrics(
        CsoCodecKind kind,
        int compressedBytes,
        int sourceBytes,
        double encodeMilliseconds,
        double decodeMilliseconds,
        bool passedRoundtrip)
    {
        double ratio = sourceBytes <= 0
            ? 0
            : (double)compressedBytes / sourceBytes;

        return new CodecTrialDecisionMetrics(
            compressedBytes,
            ratio,
            RatioGain: Math.Max(0, 1.0 - ratio),
            encodeMilliseconds,
            decodeMilliseconds,
            passedRoundtrip,
            NativeRequired: IsNativeKind(kind),
            CompatibilityRisk: GetCompatibilityRisk(kind));
    }

    private static CodecTrialCandidateResult CreateCandidateReport(
        CsoCodecKind kind,
        string name,
        int compressedBytes,
        int sourceBytes,
        double encodeMilliseconds,
        double decodeMilliseconds,
        bool passedRoundtrip,
        string? rejectedReason,
        bool selectedWinner,
        string? fallbackReason)
    {
        return new CodecTrialCandidateResult(
            name,
            GetCodecFamily(kind),
            GetLogicalLevel(kind),
            compressedBytes,
            sourceBytes <= 0 ? 0 : (double)compressedBytes / sourceBytes,
            encodeMilliseconds,
            decodeMilliseconds,
            passedRoundtrip,
            rejectedReason,
            selectedWinner,
            fallbackReason);
    }

    private static string GetCodecFamily(CsoCodecKind kind)
    {
        return kind switch
        {
            CsoCodecKind.Store => "store",
            CsoCodecKind.ManagedDeflateFastest or
                CsoCodecKind.ManagedDeflateOptimal or
                CsoCodecKind.ManagedDeflateSmallest => "managed-deflate",
            CsoCodecKind.NativeZlibDefault or
                CsoCodecKind.NativeZlibFiltered or
                CsoCodecKind.NativeZlibHuffmanOnly or
                CsoCodecKind.NativeZlibRle => "native-zlib",
            CsoCodecKind.NativeLibDeflate1 or
                CsoCodecKind.NativeLibDeflate6 or
                CsoCodecKind.NativeLibDeflate9 or
                CsoCodecKind.NativeLibDeflate12 => "native-libdeflate",
            CsoCodecKind.NativeZopfli5 or
                CsoCodecKind.NativeZopfli15 or
                CsoCodecKind.NativeZopfli25 => "native-zopfli",
            CsoCodecKind.NativeSevenZipDeflate => "native-7z-deflate-unavailable",
            _ => "unknown",
        };
    }

    private static bool IsNativeKind(CsoCodecKind kind)
    {
        return kind is CsoCodecKind.NativeZlibDefault or
            CsoCodecKind.NativeZlibFiltered or
            CsoCodecKind.NativeZlibHuffmanOnly or
            CsoCodecKind.NativeZlibRle or
            CsoCodecKind.NativeLibDeflate1 or
            CsoCodecKind.NativeLibDeflate6 or
            CsoCodecKind.NativeLibDeflate9 or
            CsoCodecKind.NativeLibDeflate12 or
            CsoCodecKind.NativeZopfli5 or
            CsoCodecKind.NativeZopfli15 or
            CsoCodecKind.NativeZopfli25 or
            CsoCodecKind.NativeSevenZipDeflate;
    }

    private static string GetCompatibilityRisk(CsoCodecKind kind)
    {
        return kind == CsoCodecKind.Store
            ? "none"
            : "standard-raw-deflate";
    }

    private static int GetLogicalLevel(CsoCodecKind kind)
    {
        return kind switch
        {
            CsoCodecKind.ManagedDeflateFastest => 1,
            CsoCodecKind.ManagedDeflateOptimal => 6,
            CsoCodecKind.ManagedDeflateSmallest => 9,
            CsoCodecKind.NativeZopfli5 => 105,
            CsoCodecKind.NativeZopfli15 => 115,
            CsoCodecKind.NativeZopfli25 => 125,
            _ => (int)kind,
        };
    }
}