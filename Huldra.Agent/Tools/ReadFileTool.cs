using Huldra.Agent.Interfaces;
using Huldra.Agent.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Nodes;

namespace Huldra.Agent.Tools;

/// <summary>
/// Tool for reading either the full content or a specific line range of a file safely inside the workspace.
/// </summary>
public class ReadFileTool : IAgentTool
{
    private readonly WorkspaceContext _context;

    public ReadFileTool(WorkspaceContext context)
    {
        _context = context;
    }

    public string Name => "read_file";

    // The description tells the LLM how to use optional parameters for context management
    public string Description => "Reads file content. To save context on large files, optionally provide 'start_line' (1-based) and 'line_count' to read a specific segment. If 'start_line' is omitted, reads the entire file.";

    public JsonObject ParametersSchema => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["path"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "The relative path of the file to read. Note: All final code and product deliverables reside inside the 'output/' directory (e.g., 'output/index.html')."
            },
            ["start_line"] = new JsonObject
            {
                ["type"] = "integer",
                ["description"] = "Optional. The 1-based start line number to begin reading. Omit to read the full file."
            },
            ["line_count"] = new JsonObject
            {
                ["type"] = "integer",
                ["description"] = "Optional. How many lines to read (default is 100 if start_line is provided)."
            }
        },
        ["required"] = new JsonArray { "path" } // Only 'path' is strictly required
    };

    public async Task<string> ExecuteAsync(string argumentsJson, CancellationToken cancellationToken = default)
    {
        try
        {
            var doc = JsonNode.Parse(argumentsJson);
            var relativePath = doc?["path"]?.ToString() ?? throw new ArgumentException("Missing parameter: 'path'");

            var safePath = PathSandboxHelper.GetSafePath(_context.RootDirectory, relativePath);
            if (!File.Exists(safePath))
            {
                return $"Error: File not found at '{relativePath}'.";
            }

            // Check if the LLM requested a partial read by checking if 'start_line' is provided
            var startLineNode = doc?["start_line"];
            if (startLineNode != null)
            {
                int startLine = startLineNode.GetValue<int>();
                int lineCount = doc?["line_count"]?.GetValue<int>() ?? 100; // Default to 100 lines if omitted

                if (startLine < 1) startLine = 1;

                var lines = new List<string>();
                var currentLineNum = 1;
                var endLine = startLine + lineCount - 1;

                using (var reader = new StreamReader(safePath))
                {
                    string? line;
                    while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
                    {
                        if (currentLineNum >= startLine && currentLineNum <= endLine)
                        {
                            lines.Add(line);
                        }
                        if (currentLineNum > endLine)
                        {
                            break;
                        }
                        currentLineNum++;
                    }
                }

                if (lines.Count == 0)
                {
                    return $"Warning: Line range {startLine}-{endLine} is out of bounds for '{relativePath}' (Total lines in file: {currentLineNum - 1}).";
                }

                return $"--- File: {relativePath} | Lines {startLine} to {startLine + lines.Count - 1} ---\n" +
                       string.Join("\n", lines) +
                       $"\n--- End of Chunk (Total lines in file: {currentLineNum - 1}) ---";
            }

            // Default behavior: Read the entire file
            var content = await File.ReadAllTextAsync(safePath, cancellationToken);
            return $"--- File: {relativePath} (Full) ---\n{content}\n--- End of File ---";
        }
        catch (Exception ex)
        {
            return $"Error executing read_file: {ex.Message}";
        }
    }
}
