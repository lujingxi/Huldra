using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Huldra.Agent.Models;

/// <summary>
/// Represents a tool call request from the LLM.
/// </summary>
public record ToolCall
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = "function";

    [JsonPropertyName("function")]
    public required FunctionCall Function { get; set; }
}
