namespace Huldra.Agent.Models;

public sealed class SecurityRule
{
    public required string Category { get; init; }

    public required SecurityDecisionKind Decision { get; init; }

    /// <summary>
    /// A locked rule cannot be overridden by a more specific policy scope.
    /// </summary>
    public bool IsLocked { get; init; }
}
