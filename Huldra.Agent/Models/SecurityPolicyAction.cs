namespace Huldra.Agent.Models;

/// <summary>
/// An action specified by a security policy.
/// Auto delegates evaluation to a dedicated Evaluator.
/// </summary>
public enum SecurityPolicyAction
{
    Allow,
    Deny,
    Ask,
    Auto
}
