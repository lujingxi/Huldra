namespace Huldra.Agent.Models;

/// <summary>
/// The result of a Runtime security evaluation.
/// </summary>
public sealed class SecurityDecision
{
    public required SecurityDecisionKind Decision { get; init; }

    public required string Source { get; init; }

    public string? ReasonCode { get; init; }

    public bool? UserApproved { get; init; }
}
