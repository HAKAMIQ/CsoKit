using CsoKit.Core.Formats.Cso.Codecs;

namespace CsoKit.Core.Formats.Cso;

public sealed class CsoCompressionWorker
{
    private const int DefaultZopfliIterations = 15;

    private readonly CsoCompressionProfileSettings settings;
    private readonly CsoBestCandidateSelector candidateSelector;
    private readonly CsoTrialEngine trialEngine;
    private readonly bool collectTrialReports;

    public CsoCompressionWorker()
        : this(
            CsoCompressionProfilePolicy.Create(CsoCompressionProfilePolicy.DefaultProfile),
            new CsoBestCandidateSelector(CsoCompressionProfilePolicy.DefaultProfile))
    {
    }

    public CsoCompressionWorker(
        CsoCompressionProfile profile,
        bool useZopfli = false,
        int zopfliIterations = DefaultZopfliIterations,
        bool collectTrialReports = false)
        : this(CsoCompressionProfilePolicy.Create(profile), new CsoBestCandidateSelector(profile), useZopfli, zopfliIterations, collectTrialReports)
    {
    }

    public CsoCompressionWorker(CsoCompressionProfileSettings settings)
        : this(settings, new CsoBestCandidateSelector(settings.Profile))
    {
    }

    public CsoCompressionWorker(
        CsoCompressionProfileSettings settings,
        CsoBestCandidateSelector candidateSelector)
        : this(settings, candidateSelector, useZopfli: false, zopfliIterations: DefaultZopfliIterations)
    {
    }

    public CsoCompressionWorker(
        CsoCompressionProfileSettings settings,
        CsoBestCandidateSelector candidateSelector,
        bool useZopfli,
        int zopfliIterations = DefaultZopfliIterations,
        bool collectTrialReports = false)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.candidateSelector = candidateSelector ?? throw new ArgumentNullException(nameof(candidateSelector));
        this.collectTrialReports = collectTrialReports;
        bool useExperimental = zopfliIterations > DefaultZopfliIterations;

        CsoTrialPlan plan = CsoTrialPlanner.CreatePlan(this.settings.Profile, useZopfli, useExperimental);
        IReadOnlyList<ICsoCodecTrial> trials = CsoTrialPlanner.CreateTrials(plan);
        trialEngine = new CsoTrialEngine(trials, this.candidateSelector);
    }

    public SectorResult Compress(SectorJob job)
    {
        SectorResult result = collectTrialReports
            ? trialEngine.CompressWithReport(job)
            : trialEngine.Compress(job);

        return result with { SourceIsAllZero = IsAllZero(job.SourceSpan) };
    }

    private static bool IsAllZero(ReadOnlySpan<byte> source)
    {
        return source.IndexOfAnyExcept((byte)0) < 0;
    }
}
