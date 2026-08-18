using Huldra.Agent.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Huldra.Agent.Services;

public class WorkspaceManager
{
    private readonly string _baseWorkspacesDir;

    public WorkspaceManager(string? customBasePath = null)
    {
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _baseWorkspacesDir = customBasePath ?? Path.Combine(userProfile, ".huldra", "workspaces");
    }

    public async Task<WorkspaceContext> InitializeWorkspaceAsync(string projectName, string? projectGoal = null)
    {
        var projectDir = Path.Combine(_baseWorkspacesDir, projectName);
        var isNew = !Directory.Exists(projectDir);

        var context = new WorkspaceContext
        {
            ProjectName = projectName,
            RootDirectory = projectDir
        };

        if (isNew)
        {
            Console.WriteLine($"[Workspace] Creating new workspace: {projectDir}");
            CreateDirectoryStructure(context);
            await InitializeFilesAsync(context, projectGoal);
            await InitializeGitAsync(projectDir);
        }
        else
        {
            Console.WriteLine($"[Workspace] Loading existing workspace: {projectDir}");
        }

        return context;
    }

    private void CreateDirectoryStructure(WorkspaceContext ctx)
    {
        Directory.CreateDirectory(ctx.RootDirectory);
        Directory.CreateDirectory(ctx.PlanDirectory);
        Directory.CreateDirectory(ctx.MemoryDirectory);
        Directory.CreateDirectory(ctx.InteractiveMemoryDirectory);
        Directory.CreateDirectory(ctx.KnowledgebaseDirectory);
        Directory.CreateDirectory(ctx.OutputDirectory);
        Directory.CreateDirectory(ctx.MemoDirectory);
        Directory.CreateDirectory(ctx.RolesDirectory);
    }

    private async Task InitializeFilesAsync(WorkspaceContext ctx, string? goal)
    {
        // Initialize combined status.md
        string statusContent = $"""
        # Workspace Status & Progress
        
        ## Project Overall Goal
        {goal ?? "Define goal here..."}

        ## Current State
        Initialized and awaiting planning.

        ## Step-by-Step Progress
        - [ ] Phase 1: Planning (Pending Conductor/Planner)
        """;
        await File.WriteAllTextAsync(ctx.StatusFilePath, statusContent);

        // Initialize plan/plan.md
        string planContent = $"""
        # Main Plan
        
        ## Overall Goal
        {goal ?? "Awaiting goal..."}

        ## Executable Phases
        Waiting for Planner to generate steps...
        """;
        await File.WriteAllTextAsync(ctx.MainPlanFilePath, planContent);
    }

    private async Task InitializeGitAsync(string targetDirectory)
    {
        try
        {
            var processInfo = new ProcessStartInfo("git", "init")
            {
                WorkingDirectory = targetDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();

                await ExecuteGitCommandAsync(targetDirectory, "add .");
                await ExecuteGitCommandAsync(targetDirectory, "commit -m \"Huldra Agent: Initialize workspace directories\"");

                Console.WriteLine("[Workspace] Git repository initialized.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Workspace Warning] Could not initialize Git: {ex.Message}");
        }
    }

    private async Task ExecuteGitCommandAsync(string workingDir, string arguments)
    {
        var pInfo = new ProcessStartInfo("git", arguments)
        {
            WorkingDirectory = workingDir,
            CreateNoWindow = true,
            UseShellExecute = false
        };
        using var process = Process.Start(pInfo);
        if (process != null) await process.WaitForExitAsync();
    }
}
