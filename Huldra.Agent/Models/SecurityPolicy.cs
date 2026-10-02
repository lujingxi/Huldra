namespace Huldra.Agent.Models;

/// <summary>
/// Security configuration for a policy scope.
/// Overall is the default for categories without an explicit rule.
/// </summary>
public sealed class SecurityPolicy
{
    public SecurityDecisionKind Overall { get; init; } = SecurityDecisionKind.Ask;

    public required IReadOnlyDictionary<string, SecurityRule> Rules { get; init; }
}
