using Huldra.Agent.Interfaces;
using Huldra.Agent.Models;
using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Huldra.Agent.Services;

/// <summary>
/// A high-performance LLM client compatible with llama.cpp server and OpenAI Chat Completion API.
/// </summary>
public class OpenAiCompatibleService : ILlmService
{
    private readonly HttpClient _httpClient;
    private readonly string _modelName;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Initializes a new instance of the LlamaCppService.
    /// </summary>
    /// <param name="baseUrl">The base URL of the running llama.cpp server (e.g., "http://localhost:8080/v1")</param>
    /// <param name="modelName">The name of the target local model</param>
    public OpenAiCompatibleService(string baseUrl = "http://localhost:8080/v1", string modelName = "local-model")
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromMinutes(15)
        };
        _modelName = modelName;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };
    }

    public async Task<ChatMessage> GetResponseAsync(
        List<ChatMessage> messages,
        List<IAgentTool>? tools = null,
        CancellationToken cancellationToken = default)
    {
        var requestPayload = new JsonObject
        {
            ["model"] = _modelName,
            ["messages"] = JsonSerializer.SerializeToNode(messages, _jsonOptions),
            ["temperature"] = 0.2 // Lower temperature for high-stability Agent workflows
        };

        // Inject tools if any are registered and supported
        if (tools != null && tools.Count > 0)
        {
            var toolsArray = new JsonArray();
            foreach (var tool in tools)
            {
                toolsArray.Add(new JsonObject
                {
                    ["type"] = "function",
                    ["function"] = new JsonObject
                    {
                        ["name"] = tool.Name,
                        ["description"] = tool.Description,
                        ["parameters"] = JsonSerializer.SerializeToNode(tool.ParametersSchema, _jsonOptions)
                    }
                });
            }
            requestPayload["tools"] = toolsArray;
        }

        // Debug: Log the request payload for inspection
        //Console.WriteLine("\n[Debug Payload] Sending JSON to LLM:");
        //Console.WriteLine(requestPayload.ToJsonString());
        //Console.WriteLine("------------------------------------\n");

        try
        {
            var response = await _httpClient.PostAsJsonAsync("chat/completions", requestPayload, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);
            var choice = responseBody?["choices"]?[0];
            var messageNode = (choice?["message"]) ?? throw new InvalidOperationException("Failed to parse LLM response format.");
            var chatMessage = JsonSerializer.Deserialize<ChatMessage>(messageNode.ToJsonString(), _jsonOptions);
            return chatMessage ?? throw new InvalidOperationException("Deserialized message is null.");
        }
        catch (Exception ex)
        {
            // Fallback / Log error
            return new ChatMessage
            {
                Role = ChatRole.Assistant,
                Content = $"[Huldra Error] LLM connection failed: {ex.Message}"
            };
        }
    }

    public async IAsyncEnumerable<string> GetResponseStreamAsync(
        List<ChatMessage> messages,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var requestPayload = new JsonObject
        {
            ["model"] = _modelName,
            ["messages"] = JsonSerializer.SerializeToNode(messages, _jsonOptions),
            ["temperature"] = 0.5, // slightly higher for conversational chat room
            ["stream"] = true
        };

        var content = new StringContent(requestPayload.ToJsonString(), Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions") { Content = content };

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        string? line;

        while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (line.StartsWith("data: [DONE]")) break;

            if (line.StartsWith("data: "))
            {
                var jsonStr = line.Substring(6);
                var node = JsonNode.Parse(jsonStr);
                var deltaContent = node?["choices"]?[0]?["delta"]?["content"]?.ToString();

                if (!string.IsNullOrEmpty(deltaContent))
                {
                    yield return deltaContent;
                }
            }
        }
    }
}
