using System;
using System.Collections.Generic;
using System.Text;

namespace Huldra.Agent.Models;

/// <summary>
/// Represents the internal state of the orchestrator, used to resume work after a shutdown.
/// This is consumed purely by the C# engine and not exposed to the LLM context.
/// </summary>
public class SessionState
{
    /// <summary>
    /// The role that needs to be executed next (e.g., "Conductor", "Planner").
    /// </summary>
    public string CurrentRole { get; set; } = "Conductor";

    /// <summary>
    /// The instructions handed over to the current active role.
    /// </summary>
    public string PendingInstructions { get; set; } = "Review the current workspace status and determine the first step to achieve the goal.";

    /// <summary>
    /// The timestamp when this session state was last saved.
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}
