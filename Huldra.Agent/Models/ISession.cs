using System;
using System.Collections.Generic;
using System.Text;

namespace Huldra.Agent.Models;

public interface ISession
{
    string Id { get; set; }
    string Name { get; set; }
    DateTime CreatedAt { get; set; }

    List<SessionMessage> Messages { get; set; }
}
