using CsoKit.Core.Compression;

namespace CsoKit.Tests.Compression;

public sealed class CsoWorkerPolicyTests
{
    [Fact]
    public void TryValidate_RejectsZeroAndValuesAboveMaximum()
    {
        int maximum = CsoWorkerPolicy.GetMaximumWorkerCount();

        Assert.False(CsoWorkerPolicy.TryValidate(0, out _));
        Assert.False(CsoWorkerPolicy.TryValidate(checked(maximum + 1), out _));
        Assert.True(CsoWorkerPolicy.TryValidate(maximum, out _));
    }

    [Fact]
    public void MaximumWorkerCount_ClampsEnvironmentOverrideToAbsoluteLimit()
    {
        string? previous = Environment.GetEnvironmentVariable(CsoWorkerPolicy.MaximumWorkersEnvironmentVariable);

        try
        {
            Environment.SetEnvironmentVariable(
                CsoWorkerPolicy.MaximumWorkersEnvironmentVariable,
                int.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture));

            Assert.Equal(
                CsoWorkerPolicy.AbsoluteMaximumWorkerCount,
                CsoWorkerPolicy.GetMaximumWorkerCount());
        }
        finally
        {
            Environment.SetEnvironmentVariable(CsoWorkerPolicy.MaximumWorkersEnvironmentVariable, previous);
        }
    }

    [Fact]
    public void QueueCapacity_IsBoundedAndChecked()
    {
        int maximum = CsoWorkerPolicy.GetMaximumWorkerCount();
        int capacity = CsoWorkerPolicy.GetBoundedQueueCapacity(maximum);

        Assert.InRange(capacity, 2, checked(CsoWorkerPolicy.AbsoluteMaximumWorkerCount * 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => CsoWorkerPolicy.GetBoundedQueueCapacity(0));
    }
}
