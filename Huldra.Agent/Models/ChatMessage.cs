using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Huldra.Agent.Models;

public enum ChatRole
{
    System,
    User,
    Assistant,
    Tool
}

/// <summary>
/// Represents a single message in the conversation.
/// </summary>
public record ChatMessage
{
    [JsonPropertyName("role")]
    public required ChatRole Role { get; set; }

    [JsonPropertyName("content")]
    public required string Content { get; set; }

    [JsonPropertyName("name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; set; }

    [JsonPropertyName("tool_call_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ToolCallId { get; set; }

    [JsonPropertyName("tool_calls")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ToolCall>? ToolCalls { get; set; }
}
