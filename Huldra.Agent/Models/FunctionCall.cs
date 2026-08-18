using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Huldra.Agent.Models;

/// <summary>
/// Represents the specific function name and arguments requested by the LLM.
/// </summary>
public record FunctionCall
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    /// <summary>
    /// JSON string of arguments.
    /// </summary>
    [JsonPropertyName("arguments")]
    public required string Arguments { get; set; }
}
