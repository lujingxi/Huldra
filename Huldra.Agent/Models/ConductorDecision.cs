using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Huldra.Agent.Models;

/// <summary>
/// Represents the decision made by the Conductor about the next step.
/// </summary>
public record ConductorDecision
{
    /// <summary>
    /// The name of the next role to activate (e.g., "Planner", "Executor", "None" if finished).
    /// </summary>
    [JsonPropertyName("next_role")]
    public required string NextRole { get; set; }

    /// <summary>
    /// Specific instructions or tasks assigned to the next role.
    /// </summary>
    [JsonPropertyName("instructions")]
    public required string Instructions { get; set; }

    /// <summary>
    /// Status update for the project (e.g., "Planning", "Executing", "Completed").
    /// </summary>
    [JsonPropertyName("status_update")]
    public string? StatusUpdate { get; set; }
}
