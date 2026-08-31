namespace Huldra.Engine.Backends;

public readonly record struct MatMulWorkloadSnapshot(
    long CallCount,
    long TotalWork,
    long TotalElapsedTicks,
    long[] WorkerWork,
    int WorkerCount);
