using System;
using System.Collections.Generic;
using System.Text;

namespace Huldra.Agent.Models;

public class ChatSession : ISession
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "New Chat";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<SessionMessage> Messages { get; set; } = [];
}
