namespace Huldra.Agent.Models;

/// <summary>
/// Identifies a role definition backed by a Markdown file.
/// Role semantics are defined by the referenced Markdown document.
/// </summary>
public sealed class Role
{
    public required string Name { get; init; }

    public required string DefinitionPath { get; init; }
}
