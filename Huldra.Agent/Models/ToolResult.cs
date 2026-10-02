namespace Huldra.Agent.Models;

public sealed class ToolResult
{
    public required bool Succeeded { get; init; }

    public required string Content { get; init; }

    public string? ErrorCode { get; init; }
}
