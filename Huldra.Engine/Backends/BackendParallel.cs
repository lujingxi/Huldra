// Huldra-Verify: 0.6.1-3
namespace Huldra.Engine.Backends;

public static class BackendParallel
{

    public static int WorkerCount => Environment.ProcessorCount;

    public static void For(
        int count,
        int minimumWorkPerPartition,
        Action<int, int> action)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfLessThan(minimumWorkPerPartition, 1);
        ArgumentNullException.ThrowIfNull(action);

        if (count == 0)
            return;

        int workerCount = CalculateWorkerCount(
            count,
            minimumWorkPerPartition);

        BackendParallelInstrumentation.Configure(
            count,
            minimumWorkPerPartition,
            workerCount);

        if (workerCount <= 1)
        {
            BackendParallelInstrumentation.RecordWorkerStart();

            try
            {
                action(0, count);
            }
            finally
            {
                BackendParallelInstrumentation.RecordWorkerEnd();
            }

            return;
        }

        Parallel.For(0, workerCount, worker =>
        {
            BackendParallelInstrumentation.RecordWorkerStart();

            try
            {
                int start = worker * count / workerCount;
                int end = (worker + 1) * count / workerCount;

                if (start < end)
                    action(start, end);
            }
            finally
            {
                BackendParallelInstrumentation.RecordWorkerEnd();
            }
        });
    }

    public static void For(
        int count,
        int minimumWorkPerPartition,
        Action<int, int, int> action)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfLessThan(
            minimumWorkPerPartition,
            1);
        ArgumentNullException.ThrowIfNull(action);

        if (count == 0)
            return;

        int workerCount = CalculateWorkerCount(
            count,
            minimumWorkPerPartition);

        BackendParallelInstrumentation.Configure(
            count,
            minimumWorkPerPartition,
            workerCount);

        if (workerCount <= 1)
        {
            BackendParallelInstrumentation.RecordWorkerStart();

            try
            {
                action(0, count, 0);
            }
            finally
            {
                BackendParallelInstrumentation.RecordWorkerEnd();
            }

            return;
        }

        Parallel.For(
            0,
            workerCount,
            worker =>
            {
                BackendParallelInstrumentation.RecordWorkerStart();

                try
                {
                    int start =
                        worker * count / workerCount;

                    int end =
                        (worker + 1) * count / workerCount;

                    if (start < end)
                        action(start, end, worker);
                }
                finally
                {
                    BackendParallelInstrumentation.RecordWorkerEnd();
                }
            });
    }

    public static int CalculateWorkerCount(
        int count,
        int minimumWorkPerPartition)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfLessThan(
            minimumWorkPerPartition,
            1);

        if (count == 0)
            return 0;

        return Math.Min(
            Environment.ProcessorCount,
            Math.Max(
                1,
                count / minimumWorkPerPartition));
    }
}
