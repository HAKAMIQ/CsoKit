using CsoKit.Core.Formats.Cso;
using CsoKit.Core.Formats.Cso.Codecs;

namespace CsoKit.Tests.Formats.Cso;

public sealed class CsoTrialPlannerTests
{
    [Fact]
    public void CreatePlan_WithGameSafe_DoesNotEnableZopfliByDefault()
    {
        CsoTrialPlan plan = CsoTrialPlanner.CreatePlan(
            CsoCompressionProfile.GameSafe,
            useZopfli: false);

        Assert.Contains(plan.Trials, trial => trial.Kind == CsoCodecKind.ManagedDeflateSmallest);
        Assert.DoesNotContain(plan.Trials, trial => trial.Kind is CsoCodecKind.NativeZopfli5 or CsoCodecKind.NativeZopfli15);
    }

    [Fact]
    public void CreatePlan_WithSmallestAndZopfli_AddsSlowZopfliCandidates()
    {
        CsoTrialPlan plan = CsoTrialPlanner.CreatePlan(
            CsoCompressionProfile.Smallest,
            useZopfli: true);

        Assert.Contains(plan.Trials, trial => trial.Kind == CsoCodecKind.NativeZopfli5 && trial.IsSlow);
        Assert.Contains(plan.Trials, trial => trial.Kind == CsoCodecKind.NativeZopfli15 && trial.IsSlow);
    }
}
