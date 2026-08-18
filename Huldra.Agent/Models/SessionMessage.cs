using System;
using System.Collections.Generic;
using System.Text;

namespace Huldra.Agent.Models;

/// <summary>
/// Represents a message in any session (Chat or Workspace).
/// Stored physically in the Huldra.Agent domain to support CLI and Server persistence.
/// </summary>
public class SessionMessage
{
    public string Sender { get; set; } = "User";
    public string Content { get; set; } = "";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public bool IsMemo { get; set; }
}
