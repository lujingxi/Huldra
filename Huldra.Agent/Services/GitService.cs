using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Huldra.Agent.Services;

/// <summary>
/// Provides automated Git version control operations within the workspace.
/// </summary>
public class GitService
{
    /// <summary>
    /// Tracks and commits all current changes in the workspace.
    /// </summary>
    public async Task<bool> CommitChangesAsync(string workspacePath, string commitMessage)
    {
        try
        {
            // 1. Stage all changes (git add .)
            var addResult = await RunGitCommandAsync(workspacePath, "add .");
            if (!addResult) return false;

            // Check if there are actually any changes to commit to avoid empty commit errors
            var statusOutput = await RunGitCommandWithOutputAsync(workspacePath, "status --porcelain");
            if (string.IsNullOrWhiteSpace(statusOutput))
            {
                // No changes to commit
                return true;
            }

            // 2. Commit changes (git commit -m "...")
            // Escape double quotes in commit message
            var safeMessage = commitMessage.Replace("\"", "\\\"");
            var commitResult = await RunGitCommandAsync(workspacePath, $"commit -m \"Huldra Auto-Commit: {safeMessage}\"");
            return commitResult;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GitService Warning] Auto-commit failed: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> RunGitCommandAsync(string workingDir, string arguments)
    {
        var processInfo = new ProcessStartInfo("git", arguments)
        {
            WorkingDirectory = workingDir,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };

        using var process = Process.Start(processInfo);
        if (process == null) return false;

        await process.WaitForExitAsync();
        return process.ExitCode == 0;
    }

    private async Task<string> RunGitCommandWithOutputAsync(string workingDir, string arguments)
    {
        var processInfo = new ProcessStartInfo("git", arguments)
        {
            WorkingDirectory = workingDir,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = Process.Start(processInfo);
        if (process == null) return string.Empty;

        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        return output.Trim();
    }
}
