using Huldra.Agent.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Huldra.Agent.Interfaces;

public interface ILlmService
{
    /// <summary>
    /// Generates a response from the LLM based on chat messages.
    /// </summary>
    Task<ChatMessage> GetResponseAsync(
        List<ChatMessage> messages,
        List<IAgentTool>? tools = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a streaming response for real-time user feedback (e.g., in Chat Room).
    /// </summary>
    IAsyncEnumerable<string> GetResponseStreamAsync(
        List<ChatMessage> messages,
        CancellationToken cancellationToken = default);
}
