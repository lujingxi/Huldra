using System;
using System.Collections.Generic;
using System.Text;

namespace Huldra.Agent.Models;

public enum AuditEventType
{
    ToolRequested,
    SecurityEvaluated,
    ToolExecutionStarted,
    ToolExecutionCompleted
}
