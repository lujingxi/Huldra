using Huldra.Agent.Interfaces;
using Huldra.Agent.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Nodes;

namespace Huldra.Agent.Tools;

/// <summary>
/// Tool for listing files recursively within the workspace directory.
/// </summary>
public class ListDirectoryTool : IAgentTool
{
    private readonly WorkspaceContext _context;

    public ListDirectoryTool(WorkspaceContext context)
    {
        _context = context;
    }

    public string Name => "list_directory";
    public string Description => "Lists all directories and files recursively in the workspace. Helps Agent map out the structure.";

    public JsonObject ParametersSchema => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject() // No required arguments, lists root by default
    };

    public Task<string> ExecuteAsync(string argumentsJson, CancellationToken cancellationToken = default)
    {
        string result;

        try
        {
            var files = Directory.GetFiles(_context.RootDirectory, "*.*", SearchOption.AllDirectories);
            var fileList = files.Select(f => Path.GetRelativePath(_context.RootDirectory, f))
                                .Where(f => !f.StartsWith(".git") && !f.Contains("node_modules")) // Skip internal folders
                                .ToList();

            if (fileList.Count == 0)
            {
                result = "The workspace is currently empty.";
            }

            result = $"Workspace File Map:\n" + string.Join("\n", fileList.Select(f => $"- {f}"));
        }
        catch (Exception ex)
        {
            result = $"Error executing list_directory: {ex.Message}";
        }

        return Task.FromResult(result);
    }
}
