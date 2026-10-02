namespace Huldra.Agent.Models;

/// <summary>
/// Runtime-generated audit information for a consequential operation.
/// </summary>
public sealed class AuditEvent
{
    public required Guid EventId { get; init; }

    public required DateTimeOffset Timestamp { get; init; }

    public string? ProjectId { get; init; }

    public string? ConversationId { get; init; }

    public string? ExecutionId { get; init; }

    public string? WorkerId { get; init; }

    public string? RoleName { get; init; }

    public string? ToolId { get; init; }

    public string? Operation { get; init; }

    public string? Target { get; init; }

    public string? SecurityPolicy { get; init; }

    public string? Decision { get; init; }

    public string? DecisionSource { get; init; }

    public bool? UserApproval { get; init; }

    public string? Result { get; init; }

    public string? ReasonCode { get; init; }
}
