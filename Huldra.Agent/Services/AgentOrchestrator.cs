using Huldra.Agent.Interfaces;
using Huldra.Agent.Models;
using Huldra.Agent.Tools;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Huldra.Agent.Services;

/// <summary>
/// Manages the autonomous agent execution loop (Loop Engineering) in Low-Compute Mode with Memo Interruptions and Session Recovery.
/// </summary>
public class AgentOrchestrator
{
    private readonly WorkspaceContext _context;
    private readonly RoleManager _roleManager;
    private readonly DynamicRoleExecutor _executor;
    private readonly MemoManager _memoManager;
    private readonly string _interactiveDir;
    private readonly List<IAgentTool> _allTools;
    private readonly Action<string>? _onLog; // Callback for streaming logs to UI

    public AgentOrchestrator(WorkspaceContext context, RoleManager roleManager, ILlmService llmService, Action<string>? onLog = null)
    {
        _context = context;
        _roleManager = roleManager;
        _executor = new DynamicRoleExecutor(llmService);
        _onLog = onLog;

        var gitService = new GitService();
        _memoManager = new MemoManager(context, gitService);
        _interactiveDir = context.InteractiveMemoryDirectory;

        _allTools =
        [
            new ReadFileTool(context),
            new WriteFileTool(context, gitService),
            new ListDirectoryTool(context),
            new ExecuteCommandTool(context, gitService),
            new PatchFileTool(context, gitService),
            new WebSearchTool(),
            new FetchWebpageTool()
        ];
    }

    private void Log(string message)
    {
        Console.WriteLine(message);
        _onLog?.Invoke(message); // Forward log stream to GUI
    }

    public async Task RunLoopAsync(CancellationToken cancellationToken = default)
    {
        Log("[Orchestrator] Initializing Loop...");

        var session = await LoadOrCreateSessionAsync();
        Log($"[Orchestrator] Session active. Resuming role: '{session.CurrentRole}'");

        bool isRunning = true;

        while (isRunning && !cancellationToken.IsCancellationRequested)
        {
            // Check for new memos
            var pendingMemo = await _memoManager.GetPendingMemoAsync();
            if (pendingMemo != null && session.CurrentRole.Equals("Conductor", StringComparison.OrdinalIgnoreCase))
            {
                Log($"\n[Memo Alert] User interruption detected! Injecting '{pendingMemo.Name}' into Conductor.");
                string memoContent = await File.ReadAllTextAsync(pendingMemo.FullName, cancellationToken);

                session.PendingInstructions = $"""
                [CRITICAL USER MEMO INTERRUPTION]
                The user has submitted an urgent Memo. You MUST address this memo immediately in this turn!
                
                User Memo Content:
                {memoContent}

                Instructions:
                1. Formulate your natural language reply to the user's questions or requests (this will be saved to their memo file).
                2. Adjust your 'next_role' routing decision. If the user wants to change strategy or refactor, you MUST call the 'Planner' next to update the plan.
                """;
            }

            var activeRole = await GetRoleDefinitionAsync(session.CurrentRole);

            if (activeRole == null)
            {
                Log($"[Orchestrator Error] Role '{session.CurrentRole}' could not be found! Falling back to Conductor.");
                session.CurrentRole = "Conductor";
                await SaveSessionAsync(session);
                continue;
            }

            // ==========================================
            // DYNAMIC TOOL ASSIGNMENT (Markdown Driven)
            // ==========================================
            List<IAgentTool> assignedTools = [];

            // If the role has a wildcard "*", grant all tools.
            if (activeRole.AllowedTools.Contains("*"))
            {
                assignedTools = _allTools;
            }
            else
            {
                // Filter the total toolbelt by matching names defined in the Role's Markdown file
                assignedTools = [.. _allTools.Where(t => activeRole.AllowedTools.Contains(t.Name, StringComparer.OrdinalIgnoreCase))];
            }

            string executionResult = await _executor.ExecuteRoleAsync(
                activeRole,
                _context,
                session.PendingInstructions,
                assignedTools,
                cancellationToken);

            string resultFileName = Path.Combine(_interactiveDir, $"{activeRole.Name.ToLower()}_output.md");
            await File.WriteAllTextAsync(resultFileName, executionResult, cancellationToken);
            Log($"[Orchestrator] '{activeRole.Name}' execution completed.");

            if (activeRole.Name.Equals("Conductor", StringComparison.OrdinalIgnoreCase))
            {
                var decision = RobustJsonParser.ExtractJson<ConductorDecision>(executionResult);

                if (decision == null)
                {
                    Log("\n[Orchestrator Warning] Conductor failed to output a valid JSON decision block!");
                    Log("======= Raw LLM Conductor Output =======");
                    Log(executionResult);
                    Log("========================================");

                    var statusContent = await File.ReadAllTextAsync(_context.StatusFilePath, cancellationToken);
                    bool isInitialPlanningPhase = statusContent.Contains("Initialized and awaiting planning", StringComparison.OrdinalIgnoreCase);

                    if (isInitialPlanningPhase)
                    {
                        Log("[Orchestrator Recovery] Forcing transition to 'Planner'.");
                        decision = new ConductorDecision
                        {
                            NextRole = "Planner",
                            Instructions = "Please read the project overall goal in status.md and create a comprehensive development plan under plan/plan.md.",
                            StatusUpdate = "Planning (Self-Healed)"
                        };
                    }
                    else
                    {
                        Log("[Orchestrator Recovery] Requesting Conductor self-correction in the next turn...");
                        session.CurrentRole = "Conductor";
                        session.PendingInstructions = "Your previous output did NOT contain a valid JSON block. You must output a JSON block wrapped in ```json ... ``` containing 'next_role', 'instructions', and 'status_update' keys at the very end of your response.";
                        await SaveSessionAsync(session);

                        await Task.Delay(2000, cancellationToken);
                        continue;
                    }
                }

                if (pendingMemo != null)
                {
                    string cleanReply = ExtractCleanReply(executionResult);
                    await _memoManager.ReplyToMemoAsync(pendingMemo, cleanReply);
                }

                if (decision.NextRole.Equals("None", StringComparison.OrdinalIgnoreCase))
                {
                    Log("[Orchestrator] Conductor decided to stop or goal has been completed.");
                    isRunning = false;

                    if (File.Exists(_context.SessionFilePath))
                    {
                        File.Delete(_context.SessionFilePath);
                    }
                }
                else
                {
                    session.CurrentRole = decision.NextRole;
                    session.PendingInstructions = decision.Instructions;

                    if (!string.IsNullOrEmpty(decision.StatusUpdate))
                    {
                        await UpdateWorkspaceStatusAsync(decision.StatusUpdate, cancellationToken);
                    }

                    string handoverFile = Path.Combine(_interactiveDir, $"conductor_to_{session.CurrentRole.ToLower()}.md");
                    await File.WriteAllTextAsync(handoverFile, session.PendingInstructions, cancellationToken);

                    await SaveSessionAsync(session);
                }
            }
            else
            {
                Log($"[Orchestrator] Role '{activeRole.Name}' finished. Returning to Conductor.");
                session.PendingInstructions = $"The role '{activeRole.Name}' has completed its task. Its output is saved in {resultFileName}. Please evaluate results and plan next step.";
                session.CurrentRole = "Conductor";

                await SaveSessionAsync(session);
            }

            await Task.Delay(1500, cancellationToken);
        }

        Log("[Orchestrator] Autonomous Loop Exited.");
    }

    private string ExtractCleanReply(string rawOutput)
    {
        var regex = new Regex(@"```json[\s\S]*?```", RegexOptions.IgnoreCase);
        string cleaned = regex.Replace(rawOutput, "").Trim();
        return cleaned;
    }

    private async Task<SessionState> LoadOrCreateSessionAsync()
    {
        if (File.Exists(_context.SessionFilePath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(_context.SessionFilePath);
                var loaded = JsonSerializer.Deserialize<SessionState>(json);
                if (loaded != null) return loaded;
            }
            catch (Exception ex)
            {
                Log($"[Orchestrator Warning] Corrupt session file found. Error: {ex.Message}");
            }
        }

        return new SessionState();
    }

    private async Task SaveSessionAsync(SessionState session)
    {
        session.LastUpdated = DateTime.UtcNow;
        var json = JsonSerializer.Serialize(session, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_context.SessionFilePath, json);
    }

    private async Task<RoleDefinition?> GetRoleDefinitionAsync(string name)
    {
        var roles = await _roleManager.LoadRolesAsync(_context.RolesDirectory);
        return roles.FirstOrDefault(r => r.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    private async Task UpdateWorkspaceStatusAsync(string status, CancellationToken cancellationToken)
    {
        string currentContent = await File.ReadAllTextAsync(_context.StatusFilePath, cancellationToken);

        string goalSection = "Define goal here...";
        if (currentContent.Contains("## Project Overall Goal"))
        {
            var parts = currentContent.Split("## Project Overall Goal");
            if (parts.Length > 1)
            {
                var goalPart = parts[1].Split("##")[0];
                goalSection = goalPart.Trim();
            }
        }

        string updatedContent = $"""
        # Workspace Status & Progress
        
        ## Project Overall Goal
        {goalSection}

        ## Current State
        {status}

        ## Last Synchronized
        {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC
        """;

        await File.WriteAllTextAsync(_context.StatusFilePath, updatedContent, cancellationToken);
    }
}
