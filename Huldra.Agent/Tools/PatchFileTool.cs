using Huldra.Agent.Interfaces;
using Huldra.Agent.Models;
using Huldra.Agent.Services;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Nodes;

namespace Huldra.Agent.Tools;

/// <summary>
/// Tool to modify specific sections of a file using search-and-replace block matching.
/// This prevents the local LLM from having to rewrite the entire massive file.
/// </summary>
public class PatchFileTool : IAgentTool
{
    private readonly WorkspaceContext _context;
    private readonly GitService _gitService;

    public PatchFileTool(WorkspaceContext context, GitService gitService)
    {
        _context = context;
        _gitService = gitService;
    }

    public string Name => "patch_file";
    public string Description => "Modifies a specific part of a file. Search for 'search_block' and replace it with 'replace_block'. Both must match formatting exactly.";

    public JsonObject ParametersSchema => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["path"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "The relative path of the file to modify. IMPORTANT: All final code, HTML, assets, or project deliverables reside strictly inside the 'output/' directory (e.g., 'output/index.html')."
            },
            ["search_block"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "The exact original text block currently in the file that you want to replace"
            },
            ["replace_block"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "The new text block that will replace the original text block"
            }
        },
        ["required"] = new JsonArray { "path", "search_block", "replace_block" }
    };

    public async Task<string> ExecuteAsync(string argumentsJson, CancellationToken cancellationToken = default)
    {
        try
        {
            var doc = JsonNode.Parse(argumentsJson);
            var relativePath = doc?["path"]?.ToString() ?? throw new ArgumentException("Missing parameter: 'path'");
            var searchBlock = doc?["search_block"]?.ToString() ?? throw new ArgumentException("Missing parameter: 'search_block'");
            var replaceBlock = doc?["replace_block"]?.ToString() ?? throw new ArgumentException("Missing parameter: 'replace_block'");

            var safePath = PathSandboxHelper.GetSafePath(_context.RootDirectory, relativePath);
            if (!File.Exists(safePath))
            {
                return $"Error: File to patch not found at '{relativePath}'.";
            }

            var originalContent = await File.ReadAllTextAsync(safePath, cancellationToken);

            // Normalize line endings to ensure bulletproof match across Windows (CRLF) and Unix (LF)
            var normalizedContent = originalContent.Replace("\r\n", "\n");
            var normalizedSearch = searchBlock.Replace("\r\n", "\n");
            var normalizedReplace = replaceBlock.Replace("\r\n", "\n");

            // Perform safety check to make sure the target search block exists and is unique
            int firstOccurrence = normalizedContent.IndexOf(normalizedSearch, StringComparison.Ordinal);
            if (firstOccurrence == -1)
            {
                return $"Error: Could not find the exact 'search_block' inside '{relativePath}'. " +
                       "Please check your indentation, spacing, and ensure the target text matches exactly.";
            }

            int secondOccurrence = normalizedContent.IndexOf(normalizedSearch, firstOccurrence + normalizedSearch.Length, StringComparison.Ordinal);
            if (secondOccurrence != -1)
            {
                return $"Error: Multiple matches of the 'search_block' were found inside '{relativePath}'. " +
                       "Please include more surrounding context lines in your 'search_block' to make it unique.";
            }

            // Execute search and replace
            var patchedContent = normalizedContent.Replace(normalizedSearch, normalizedReplace);

            // Restore original platform line endings if needed (keep CRLF on Windows for C# files)
            if (originalContent.Contains("\r\n"))
            {
                patchedContent = patchedContent.Replace("\n", "\r\n");
            }

            await File.WriteAllTextAsync(safePath, patchedContent, cancellationToken);

            // Auto commit under git
            await _gitService.CommitChangesAsync(_context.RootDirectory, $"Patched file section: {relativePath}");

            return $"Success: Successfully patched '{relativePath}'. Modified block replaced.";
        }
        catch (Exception ex)
        {
            return $"Error executing patch_file: {ex.Message}";
        }
    }
}
