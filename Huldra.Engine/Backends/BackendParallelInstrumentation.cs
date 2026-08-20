namespace Huldra.Engine.Backends;

using System.Collections.Concurrent;
using System.Threading;

public sealed record BackendParallelStats(
    int RequestedWorkItems,
    int MinimumWorkPerPartition,
    int WorkerCount,
    int MaxConcurrentWorkers,
    int DistinctManagedThreads,
    int DistinctLogicalProcessors);

public static class BackendParallelInstrumentation
{
    private static int _enabled;

    private static readonly AsyncLocal<Collector?> CurrentCollector = new();

    public static bool Enabled
    {
        get => Volatile.Read(ref _enabled) != 0;
        set => Volatile.Write(ref _enabled, value ? 1 : 0);
    }

    public static BackendParallelStats? LastStats { get; private set; }

    public static IDisposable Begin()
    {
        Collector collector = new();
        CurrentCollector.Value = collector;

        return new Scope(collector);
    }

    internal static void RecordWorkerStart()
    {
        if (!Enabled)
            return;

        Collector? collector = CurrentCollector.Value;
        if (collector is null)
            return;

        collector.WorkerStarted();
    }

    internal static void RecordWorkerEnd()
    {
        if (!Enabled)
            return;

        Collector? collector = CurrentCollector.Value;
        if (collector is null)
            return;

        collector.WorkerEnded();
    }

    private sealed class Scope : IDisposable
    {
        private readonly Collector _collector;
        private bool _disposed;

        public Scope(Collector collector)
        {
            _collector = collector;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            LastStats = _collector.CreateStats();
            CurrentCollector.Value = null;
        }
    }

    private sealed class Collector
    {
        private int _activeWorkers;
        private int _maxConcurrentWorkers;

        private readonly ConcurrentDictionary<int, byte> _managedThreads = new();
        private readonly ConcurrentDictionary<int, byte> _logicalProcessors = new();

        private int _requestedWorkItems;
        private int _minimumWorkPerPartition;
        private int _workerCount;

        public void Configure(
            int requestedWorkItems,
            int minimumWorkPerPartition,
            int workerCount)
        {
            _requestedWorkItems = requestedWorkItems;
            _minimumWorkPerPartition = minimumWorkPerPartition;
            _workerCount = workerCount;
        }

        public void WorkerStarted()
        {
            int active = Interlocked.Increment(ref _activeWorkers);

            int previousMax;
            do
            {
                previousMax = Volatile.Read(ref _maxConcurrentWorkers);

                if (active <= previousMax)
                    break;
            }
            while (Interlocked.CompareExchange(
                       ref _maxConcurrentWorkers,
                       active,
                       previousMax) != previousMax);

            int managedThreadId = Environment.CurrentManagedThreadId;
            _managedThreads.TryAdd(managedThreadId, 0);

            int processorId = Thread.GetCurrentProcessorId();
            _logicalProcessors.TryAdd(processorId, 0);
        }

        public void WorkerEnded()
        {
            Interlocked.Decrement(ref _activeWorkers);
        }

        public BackendParallelStats CreateStats()
        {
            return new BackendParallelStats(
                _requestedWorkItems,
                _minimumWorkPerPartition,
                _workerCount,
                Volatile.Read(ref _maxConcurrentWorkers),
                _managedThreads.Count,
                _logicalProcessors.Count);
        }
    }

    internal static void Configure(
        int requestedWorkItems,
        int minimumWorkPerPartition,
        int workerCount)
    {
        if (!Enabled)
            return;

        CurrentCollector.Value?.Configure(
            requestedWorkItems,
            minimumWorkPerPartition,
            workerCount);
    }
}
