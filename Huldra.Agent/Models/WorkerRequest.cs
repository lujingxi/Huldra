namespace Huldra.Agent.Models;

/// <summary>
/// A request to create a Worker.
/// Requested tools are capabilities requested by the requester; Runtime decides
/// which capabilities are actually granted.
/// </summary>
public sealed class WorkerRequest
{
    public required Role Role { get; init; }

    public required string ModelId { get; init; }

    public required IReadOnlyList<string> RequestedTools { get; init; }

    public required string Task { get; init; }
}
