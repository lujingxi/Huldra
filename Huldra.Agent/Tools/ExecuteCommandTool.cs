using Huldra.Agent.Interfaces;
using Huldra.Agent.Models;
using Huldra.Agent.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;

namespace Huldra.Agent.Tools;

/// <summary>
/// Secure execution tool to run CLI commands (like dotnet, git) inside the 'output' directory.
/// </summary>
public class ExecuteCommandTool : IAgentTool
{
    private readonly WorkspaceContext _context;
    private readonly GitService _gitService;

    // Destructive keywords and commands that the Agent is strictly forbidden to run.
    private static readonly string[] RestrictedKeywords =
    {
        "rm -rf", "rmdir", "format", "git clean", "git reset --hard", "fdisk", "del /f"
    };

    public ExecuteCommandTool(WorkspaceContext context, GitService gitService)
    {
        _context = context;
        _gitService = gitService;
    }

    public string Name => "execute_command";
    public string Description => "Runs commands inside the workspace 'output' folder. Safe allowed commands: dotnet, git status, git diff, etc.";

    public JsonObject ParametersSchema => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["command"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "The base executable command (e.g., 'dotnet')"
            },
            ["arguments"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "Arguments for the command (e.g., 'new console -n App')"
            }
        },
        ["required"] = new JsonArray { "command" }
    };

    public async Task<string> ExecuteAsync(string argumentsJson, CancellationToken cancellationToken = default)
    {
        try
        {
            var doc = JsonNode.Parse(argumentsJson);
            var command = doc?["command"]?.ToString() ?? throw new ArgumentException("Missing parameter: 'command'");
            var arguments = doc?["arguments"]?.ToString() ?? string.Empty;

            var fullCommandLine = $"{command} {arguments}".Trim();

            // Safety inspection
            foreach (var restriction in RestrictedKeywords)
            {
                if (fullCommandLine.Contains(restriction, StringComparison.OrdinalIgnoreCase))
                {
                    return $"Access Denied: Execution blocked! Command contains prohibited destructive operator: '{restriction}'.";
                }
            }

            // Commands should execute inside the 'output' folder of the workspace
            var workingDir = _context.OutputDirectory;
            Directory.CreateDirectory(workingDir); // Ensure output directory exists

            var processInfo = new ProcessStartInfo(command, arguments)
            {
                WorkingDirectory = workingDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process == null)
            {
                return "Error: Failed to start process execution.";
            }

            // Await output safely with timeout boundary
            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);

            var output = await outputTask;
            var error = await errorTask;

            // Commit any filesystem mutations resulting from the command
            await _gitService.CommitChangesAsync(_context.RootDirectory, $"Executed command: {fullCommandLine}");

            var result = $"Command Exit Code: {process.ExitCode}\n";
            if (!string.IsNullOrEmpty(output)) result += $"[Stdout]\n{output}\n";
            if (!string.IsNullOrEmpty(error)) result += $"[Stderr]\n{error}\n";

            return result;
        }
        catch (Exception ex)
        {
            return $"Error executing command: {ex.Message}";
        }
    }
}
