using Huldra.Agent.Interfaces;
using Huldra.Agent.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Huldra.Agent.Services;

/// <summary>
/// Executes a dynamic role's behavior, safeguarding against infinite loops while permitting heavy multi-file operations.
/// </summary>
public class DynamicRoleExecutor
{
    private readonly ILlmService _llmService;
    private const int MaxGlobalLoops = 100;
    private const int MaxDuplicateAllowed = 4;

    public DynamicRoleExecutor(ILlmService llmService)
    {
        _llmService = llmService;
    }

    public async Task<string> ExecuteRoleAsync(
        RoleDefinition role,
        WorkspaceContext context,
        string inputInstructions,
        List<IAgentTool>? tools = null,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"\n[Role Activation] Activating dynamic role: {role.Name}");

        string statusContent = await SafeReadFileAsync(context.StatusFilePath, "No workspace status recorded.");
        var systemPrompt = BuildRoleSystemPrompt(role);

        var userContent = $"""
        # Workspace Context
        ## Current Status & Progress
        {statusContent}

        # Input Task / Instructions for You:
        {inputInstructions}

        Please execute your task. Write any necessary output files to the workspace directory.
        Provide your summary response below:
        """;

        var messages = new List<ChatMessage>
        {
            new() { Role = ChatRole.System, Content = systemPrompt },
            new() { Role = ChatRole.User, Content = userContent }
        };

        var response = await _llmService.GetResponseAsync(messages, tools, cancellationToken);

        int loopCount = 0;
        var toolInvocationHistory = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        // Loop until LLM stops calling tools or hits a safeguard
        while (response.ToolCalls != null && response.ToolCalls.Count > 0 && tools != null && tools.Count > 0)
        {
            loopCount++;
            if (loopCount > MaxGlobalLoops)
            {
                Console.WriteLine($"[DynamicRoleExecutor Warning] Exceeded maximum global tool execution turns ({MaxGlobalLoops}). Breaking loop.");
                break;
            }

            messages.Add(response);
            bool shouldAbortDueToLoop = false;

            foreach (var toolCall in response.ToolCalls)
            {
                // Generate unique signature (ToolName + arguments)
                string signature = $"{toolCall.Function.Name}:{toolCall.Function.Arguments.Trim()}";

                if (toolInvocationHistory.TryGetValue(signature, out int count))
                {
                    toolInvocationHistory[signature] = count + 1;
                    if (toolInvocationHistory[signature] > MaxDuplicateAllowed)
                    {
                        Console.WriteLine($"\n[Loop Protection] Endless loop detected on tool invocation: '{signature}'!");
                        Console.WriteLine("[Loop Protection] Triggering safe recovery mechanism.");
                        shouldAbortDueToLoop = true;
                        break;
                    }
                }
                else
                {
                    toolInvocationHistory[signature] = 1;
                }

                var targetTool = tools.FirstOrDefault(t => t.Name.Equals(toolCall.Function.Name, StringComparison.OrdinalIgnoreCase));
                string executionResult;

                if (targetTool != null)
                {
                    Console.WriteLine($"[Tool Invocation] LLM requested '{targetTool.Name}' with arguments: {toolCall.Function.Arguments}");
                    executionResult = await targetTool.ExecuteAsync(toolCall.Function.Arguments, cancellationToken);
                }
                else
                {
                    executionResult = $"Error: Tool '{toolCall.Function.Name}' was not found.";
                }

                messages.Add(new ChatMessage
                {
                    Role = ChatRole.Tool,
                    ToolCallId = toolCall.Id,
                    Name = toolCall.Function.Name,
                    Content = executionResult
                });
            }

            // ========================================================
            // SMART CYCLE SELF-HEALING RECOVERY
            // If an endless loop is detected, we strip the LLM's tools access 
            // and force it to wrap up with a final textual/JSON response.
            // ========================================================
            if (shouldAbortDueToLoop)
            {
                Console.WriteLine("[Loop Protection] Requesting final clean response from LLM by disabling tools...");

                messages.Add(new ChatMessage
                {
                    Role = ChatRole.System,
                    Content = "System Alert: You have entered an endless loop of duplicate tool calls. You are strictly FORBIDDEN from calling any more tools in this turn. You MUST immediately write your final response, complete your textual analysis, and output the required JSON decision block based on the information you already gathered."
                });

                // Call LLM with tools set to null -> Forces model to output text/JSON immediately
                response = await _llmService.GetResponseAsync(messages, null, cancellationToken);
                break; // Break out of the loop with the clean final response
            }

            response = await _llmService.GetResponseAsync(messages, tools, cancellationToken);
        }

        return response.Content;
    }

    private string BuildRoleSystemPrompt(RoleDefinition role)
    {
        var prompt = role.SystemPrompt;

        if (role.Name.Equals("Conductor", StringComparison.OrdinalIgnoreCase))
        {
            prompt += "\n\nCRITICAL OUTPUT CONSTRAINT:\n" +
                      "You MUST begin your response immediately with the JSON block matching the structure below.\n" +
                      "Do NOT write any introduction, greetings, thoughts, or markdown formatting before this JSON block.\n" +
                      "Only AFTER you have fully closed the JSON block with '```', you can write your natural language analysis, thoughts, and reasoning.\n\n" +
                      "```json\n" +
                      "{\n" +
                      "  \"next_role\": \"Planner|Executor|Researcher|Evaluator|None\",\n" +
                      "  \"instructions\": \"detailed tasks for the next role...\",\n" +
                      "  \"status_update\": \"brief status of the project\"\n" +
                      "}\n" +
                      "```";
        }

        return prompt;
    }

    private async Task<string> SafeReadFileAsync(string path, string fallback)
    {
        if (!File.Exists(path)) return fallback;
        return await File.ReadAllTextAsync(path);
    }
}
