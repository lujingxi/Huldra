using System;
using System.Collections.Generic;
using System.Text;

namespace Huldra.Agent.Models;

public class RoleDefinition
{
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required string SystemPrompt { get; set; }
    public bool IsCoreRole { get; set; } = false;
    public List<string> AllowedTools { get; set; } = [];
}
