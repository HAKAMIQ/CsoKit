namespace CsoKit.Core.Compression;

public static class CsoWorkerPolicy
{
    public const string MaximumWorkersEnvironmentVariable = "CSOKIT_MAX_WORKERS";
    public const int AbsoluteMaximumWorkerCount = 64;

    public static int GetDefaultWorkerCount()
    {
        return Math.Min(Environment.ProcessorCount, GetMaximumWorkerCount());
    }

    public static int GetMaximumWorkerCount()
    {
        int processorScaled = Environment.ProcessorCount > AbsoluteMaximumWorkerCount / 2
            ? AbsoluteMaximumWorkerCount
            : Environment.ProcessorCount * 2;
        int recommended = Math.Clamp(processorScaled, 4, AbsoluteMaximumWorkerCount);
        string? configured = Environment.GetEnvironmentVariable(MaximumWorkersEnvironmentVariable)?.Trim();

        if (!int.TryParse(configured, out int requested))
        {
            return recommended;
        }

        return Math.Clamp(requested, 1, AbsoluteMaximumWorkerCount);
    }

    public static bool TryValidate(int workerCount, out string? errorMessage)
    {
        int maximum = GetMaximumWorkerCount();

        if (workerCount < 1)
        {
            errorMessage = "Compression worker count must be greater than zero.";
            return false;
        }

        if (workerCount > maximum)
        {
            errorMessage = $"Compression worker count {workerCount:N0} exceeds the configured maximum of {maximum:N0}.";
            return false;
        }

        errorMessage = null;
        return true;
    }

    public static int GetBoundedQueueCapacity(int workerCount)
    {
        if (!TryValidate(workerCount, out string? errorMessage))
        {
            throw new ArgumentOutOfRangeException(nameof(workerCount), workerCount, errorMessage);
        }

        return checked(Math.Max(2, workerCount * 2));
    }
}
