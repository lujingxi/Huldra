namespace Huldra.Agent.Models;

/// <summary>
/// Runtime-constructed working context supplied to a Worker.
/// Persistent conversation history is not owned by this object.
/// </summary>
public sealed class WorkerContext
{
    public required IReadOnlyList<ContextMessage> Messages { get; init; }

    public int? TokenBudget { get; init; }
}
