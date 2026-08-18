using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Nodes;

namespace Huldra.Agent.Interfaces;

/// <summary>
/// Defines a tool that can be registered and called dynamically by the AI Agent.
/// </summary>
public interface IAgentTool
{
    /// <summary>
    /// The unique name of the tool (e.g., "web_search", "execute_command").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// A clear description of what the tool does. Used by LLM to decide when to call it.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// The JSON Schema of the parameters this tool accepts.
    /// </summary>
    JsonObject ParametersSchema { get; }

    /// <summary>
    /// Executes the tool with the provided arguments.
    /// </summary>
    /// <param name="argumentsJson">JSON string of arguments passed by the LLM.</param>
    /// <returns>Execution result as a string.</returns>
    Task<string> ExecuteAsync(string argumentsJson, CancellationToken cancellationToken = default);
}
