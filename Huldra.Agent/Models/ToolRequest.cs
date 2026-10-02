namespace Huldra.Agent.Models;

/// <summary>
/// A request from a Worker to invoke a Tool.
/// Runtime is responsible for authorization and execution.
/// </summary>
public sealed class ToolRequest
{
    public required string ToolId { get; init; }

    /// <summary>
    /// JSON object containing the Tool's arguments.
    /// </summary>
    public required string ArgumentsJson { get; init; }
}
