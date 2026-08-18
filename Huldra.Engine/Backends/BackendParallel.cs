namespace Huldra.Engine.Backends;

public static class BackendParallel
{
    public static void For(int count, int minimumWorkPerPartition, Action<int, int> action)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfLessThan(minimumWorkPerPartition, 1);
        ArgumentNullException.ThrowIfNull(action);

        if (count == 0)
            return;

        int workerCount = Math.Min(Environment.ProcessorCount, Math.Max(1, count / minimumWorkPerPartition));
        if (workerCount <= 1)
        {
            action(0, count);
            return;
        }

        Parallel.For(0, workerCount, worker =>
        {
            int start = worker * count / workerCount;
            int end = (worker + 1) * count / workerCount;
            if (start < end)
                action(start, end);
        });
    }
}
