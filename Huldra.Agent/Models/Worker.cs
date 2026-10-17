using System.Collections.Frozen;

namespace Huldra.Agent.Models;

/// <summary>
/// A Worker created and authorized by Runtime.
/// </summary>
public sealed class Worker
{
    public required string Id { get; init; }

    public required Role Role { get; init; }

    public required string ModelId { get; init; }

    /// <summary>
    /// Tool identifiers actually granted by Runtime.
    /// </summary>
    public required FrozenSet<string> GrantedTools { get; init; }

    public required WorkerContext Context { get; init; }

    public WorkerStatus Status { get; internal set; } = WorkerStatus.Created;
}
