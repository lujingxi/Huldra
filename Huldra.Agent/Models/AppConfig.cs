using System;
using System.Collections.Generic;
using System.Text;

namespace Huldra.Agent.Models;

/// <summary>
/// Explicit application configuration stored in config.json.
/// Managed by the user or the application.
/// </summary>
public class AppConfig
{
    public string LlmBaseUrl { get; set; } = "http://localhost:8080/v1";
    public string LlmModelName { get; set; } = "local-model";
    public int MaxToolLoops { get; set; } = 100;

    // Future explicit settings (e.g., Theme = "Auto", Language = "en")
}
