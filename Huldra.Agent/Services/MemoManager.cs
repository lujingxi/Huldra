using Huldra.Agent.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Huldra.Agent.Services;

/// <summary>
/// Manages asynchronous user interruptions and requests via Markdown Memos.
/// </summary>
public class MemoManager
{
    private readonly WorkspaceContext _context;
    private readonly GitService _gitService;

    public MemoManager(WorkspaceContext context, GitService gitService)
    {
        _context = context;
        _gitService = gitService;
    }

    /// <summary>
    /// Scans the memo directory for any files that have not been responded to yet.
    /// An unprocessed memo is a file ending in .md that does not contain an "## Agent Reply" section.
    /// </summary>
    public async Task<FileInfo?> GetPendingMemoAsync()
    {
        if (!Directory.Exists(_context.MemoDirectory)) return null;

        var directoryInfo = new DirectoryInfo(_context.MemoDirectory);
        var memoFiles = directoryInfo.GetFiles("*.md")
                                     .OrderBy(f => f.CreationTime)
                                     .ToList();

        foreach (var file in memoFiles)
        {
            var content = await File.ReadAllTextAsync(file.FullName);

            // Check if the file has already been processed by the Agent
            if (!content.Contains("## Agent Reply", StringComparison.OrdinalIgnoreCase))
            {
                return file;
            }
        }

        return null;
    }

    /// <summary>
    /// Appends the Agent's answer and command execution result to the user's memo file.
    /// </summary>
    public async Task ReplyToMemoAsync(FileInfo memoFile, string agentResponse)
    {
        var replyBlock = $"\n\n## Agent Reply\n*Responded at: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC*\n\n{agentResponse}\n";

        await File.AppendAllTextAsync(memoFile.FullName, replyBlock);

        // Auto Git track
        await _gitService.CommitChangesAsync(_context.RootDirectory, $"Responded to memo: {memoFile.Name}");

        Console.WriteLine($"[MemoManager] Successfully replied to {memoFile.Name}");
    }
}
