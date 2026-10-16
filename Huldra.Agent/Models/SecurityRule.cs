namespace Huldra.Agent.Models;

/// <summary>
/// Defines the decision for a security category.
/// </summary>
public sealed class SecurityRule
{
    public required SecurityDecisionKind Decision { get; init; }

    /// <summary>
    /// A locked rule cannot be overridden by a more specific policy scope.
    /// </summary>
    public bool IsLocked { get; init; }
}
