using Huldra.Agent.Interfaces;
using Huldra.Agent.Models;
using Huldra.Agent.Services;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Nodes;

namespace Huldra.Agent.Tools;

/// <summary>
/// Tool for writing or creating a file safely inside the workspace.
/// </summary>
public class WriteFileTool : IAgentTool
{
    private readonly WorkspaceContext _context;
    private readonly GitService _gitService;

    public WriteFileTool(WorkspaceContext context, GitService gitService)
    {
        _context = context;
        _gitService = gitService;
    }

    public string Name => "write_file";
    public string Description => "Creates or overwrites a single file with content within the workspace. Input should contain relative 'path' and 'content'.";

    public JsonObject ParametersSchema => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["path"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "The relative path of the file to write. IMPORTANT: All final code, HTML, assets, or project deliverables MUST be placed strictly inside the 'output/' directory (e.g., 'output/index.html')."
            },
            ["content"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "The full raw text content to write into the file"
            }
        },
        ["required"] = new JsonArray { "path", "content" }
    };

    public async Task<string> ExecuteAsync(string argumentsJson, CancellationToken cancellationToken = default)
    {
        try
        {
            var doc = JsonNode.Parse(argumentsJson);
            var relativePath = doc?["path"]?.ToString() ?? throw new ArgumentException("Missing required parameter: 'path'");
            var content = doc?["content"]?.ToString() ?? throw new ArgumentException("Missing required parameter: 'content'");

            var safePath = PathSandboxHelper.GetSafePath(_context.RootDirectory, relativePath);

            // Ensure parent directory exists
            var directoryName = Path.GetDirectoryName(safePath);
            if (directoryName != null)
            {
                Directory.CreateDirectory(directoryName);
            }

            // Write content
            await File.WriteAllTextAsync(safePath, content, cancellationToken);

            // Auto Git Commit for safety tracking
            await _gitService.CommitChangesAsync(_context.RootDirectory, $"Modified file: {relativePath}");

            return $"Success: File written successfully to '{relativePath}'.";
        }
        catch (Exception ex)
        {
            return $"Error executing write_file: {ex.Message}";
        }
    }
}
