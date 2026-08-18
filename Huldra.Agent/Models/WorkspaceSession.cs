using System;
using System.Collections.Generic;
using System.Text;

namespace Huldra.Agent.Models;

public class WorkspaceSession : ISession
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "New Workspace";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string ProjectGoal { get; set; } = "";
    public List<SessionMessage> Messages { get; set; } = [];
}
