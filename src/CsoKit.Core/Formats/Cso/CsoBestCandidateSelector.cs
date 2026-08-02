namespace CsoKit.Core.Formats.Cso;

public sealed class CsoBestCandidateSelector
{
    private const double FastNearTieRatio = 0.01;
    private const int FastNearTieMinimumBytes = 8;
    private const double MeaningfulCostFactor = 1.25;

    private readonly CsoCompressionProfile profile;

    public CsoBestCandidateSelector()
        : this(CsoCompressionProfile.GameSafe)
    {
    }

    public CsoBestCandidateSelector(CsoCompressionProfile profile)
    {
        if (!Enum.IsDefined(profile))
        {
            throw new ArgumentOutOfRangeException(nameof(profile), profile, "Compression profile is not supported.");
        }

        this.profile = profile;
    }

    public static SectorResult Select(SectorJob job, SectorResult candidate)
    {
        ValidateCandidate(job, candidate);

        if (candidate.OutputLength >= job.SourceLength)
        {
            return CreateStoredResult(job);
        }

        return candidate;
    }

    public SectorResult Select(SectorJob job, IReadOnlyList<SectorResult> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        SectorResult? bestCandidate = null;

        for (int index = 0; index < candidates.Count; index++)
        {
            SectorResult candidate = candidates[index];
            ValidateCandidate(job, candidate);

            if (bestCandidate is null || IsBetterCandidate(job, candidate, bestCandidate))
            {
                bestCandidate = candidate;
            }
        }

        if (bestCandidate is null ||
            (!bestCandidate.IsStored && bestCandidate.OutputLength >= job.SourceLength))
        {
            return CreateStoredResult(job);
        }

        return bestCandidate;
    }

    public static SectorResult CreateStoredResult(SectorJob job)
    {
        byte[] storedBuffer = job.SourceSpan.ToArray();

        return new SectorResult(
            job.BlockIndex,
            job.SourceOffset,
            job.SourceLength,
            storedBuffer.Length,
            IsStored: true,
            Method: CompressionMethod.Store,
            Level: 0,
            Buffer: storedBuffer,
            CodecName: "store",
            DecisionMetrics: new(
                storedBuffer.Length,
                Ratio: 1.0,
                RatioGain: 0,
                EncodeMilliseconds: 0,
                DecodeMilliseconds: 0,
                PassedRoundtrip: true,
                NativeRequired: false,
                CompatibilityRisk: "none"));
    }

    private bool IsBetterCandidate(
        SectorJob job,
        SectorResult candidate,
        SectorResult currentBest)
    {
        if (profile == CsoCompressionProfile.Fast &&
            TryFastNearTieDecision(job, candidate, currentBest, out bool candidateWins))
        {
            return candidateWins;
        }

        if (candidate.OutputLength != currentBest.OutputLength)
        {
            return candidate.OutputLength < currentBest.OutputLength;
        }

        if (candidate.IsStored != currentBest.IsStored)
        {
            return candidate.IsStored;
        }

        return candidate.Level > currentBest.Level;
    }

    private static bool TryFastNearTieDecision(
        SectorJob job,
        SectorResult candidate,
        SectorResult currentBest,
        out bool candidateWins)
    {
        candidateWins = false;

        int byteDelta = Math.Abs(candidate.OutputLength - currentBest.OutputLength);
        int tolerance = Math.Max(
            FastNearTieMinimumBytes,
            checked((int)Math.Ceiling(job.SourceLength * FastNearTieRatio)));

        if (byteDelta > tolerance)
        {
            return false;
        }

        double candidateCost = GetDecisionCost(candidate);
        double currentCost = GetDecisionCost(currentBest);

        if (candidate.OutputLength == currentBest.OutputLength)
        {
            if (!NearlyEqual(candidateCost, currentCost))
            {
                candidateWins = candidateCost < currentCost;
                return true;
            }

            return false;
        }

        if (candidate.OutputLength < currentBest.OutputLength)
        {
            candidateWins = candidateCost <= currentCost * MeaningfulCostFactor;
            return true;
        }

        candidateWins = candidateCost * MeaningfulCostFactor < currentCost;
        return true;
    }

    private static double GetDecisionCost(SectorResult result)
    {
        if (result.DecisionMetrics is null)
        {
            return EstimateLogicalCodecCost(result);
        }

        double measuredCost = result.DecisionMetrics.EncodeMilliseconds +
            (result.DecisionMetrics.DecodeMilliseconds * 2);

        if (result.DecisionMetrics.NativeRequired)
        {
            measuredCost += 0.01;
        }

        if (!string.Equals(result.DecisionMetrics.CompatibilityRisk, "standard-raw-deflate", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(result.DecisionMetrics.CompatibilityRisk, "none", StringComparison.OrdinalIgnoreCase))
        {
            measuredCost += 1.0;
        }

        return measuredCost;
    }

    private static double EstimateLogicalCodecCost(SectorResult result)
    {
        if (result.IsStored)
        {
            return 0;
        }

        if (result.Method == CompressionMethod.ZopfliDeflate)
        {
            return 100 + Math.Max(0, result.Level);
        }

        if (result.EffectiveCodecName.Contains("libdeflate-1", StringComparison.OrdinalIgnoreCase) ||
            result.EffectiveCodecName.Contains("fastest", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        return Math.Max(2, result.Level);
    }

    private static bool NearlyEqual(double left, double right)
    {
        return Math.Abs(left - right) <= 0.000_001;
    }

    private static void ValidateCandidate(SectorJob job, SectorResult candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        if (candidate.BlockIndex != job.BlockIndex)
        {
            throw new InvalidDataException("Compression candidate block index does not match the sector job.");
        }

        if (candidate.SourceOffset != job.SourceOffset)
        {
            throw new InvalidDataException("Compression candidate source offset does not match the sector job.");
        }

        if (candidate.SourceLength != job.SourceLength)
        {
            throw new InvalidDataException("Compression candidate source length does not match the sector job.");
        }

        if (candidate.OutputLength < 0 || candidate.OutputLength > candidate.Buffer.Length)
        {
            throw new InvalidDataException("Compression candidate output length is invalid.");
        }
    }
}