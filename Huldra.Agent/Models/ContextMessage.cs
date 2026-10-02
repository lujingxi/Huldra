namespace Huldra.Agent.Models;

/// <summary>
/// A language-model-independent message in a Worker's working context.
/// </summary>
public sealed class ContextMessage
{
    public required string Role { get; init; }

    public required string Content { get; init; }
}
