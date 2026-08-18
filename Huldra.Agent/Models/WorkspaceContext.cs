using System;
using System.Collections.Generic;
using System.Text;

namespace Huldra.Agent.Models;

/// <summary>
/// Holds paths and context details for a specific workspace.
/// </summary>
public record WorkspaceContext
{
    public required string ProjectName { get; init; }
    public required string RootDirectory { get; init; }

    // Core state and tracking files
    public string StatusFilePath => Path.Combine(RootDirectory, "status.md"); // Combined state and progress
    public string SessionFilePath => Path.Combine(RootDirectory, "session.json"); // C# Engine persistence state

    // Core directories
    public string PlanDirectory => Path.Combine(RootDirectory, "plan");
    public string MemoryDirectory => Path.Combine(RootDirectory, "memory");
    public string InteractiveMemoryDirectory => Path.Combine(MemoryDirectory, "interactive");
    public string KnowledgebaseDirectory => Path.Combine(RootDirectory, "knowledgebase");
    public string OutputDirectory => Path.Combine(RootDirectory, "output");
    public string MemoDirectory => Path.Combine(RootDirectory, "memo");
    public string RolesDirectory => Path.Combine(RootDirectory, "roles");

    // Main overall plan file
    public string MainPlanFilePath => Path.Combine(PlanDirectory, "plan.md");
}
