using System;
using System.Collections.Generic;
using System.Text;

namespace Huldra.Agent.Tools;

/// <summary>
/// Helper class to enforce security boundaries (sandbox) on the filesystem.
/// </summary>
public static class PathSandboxHelper
{
    public static string GetSafePath(string workspaceRoot, string relativePath)
    {
        var resolvedRoot = Path.GetFullPath(workspaceRoot);
        var combinedPath = Path.GetFullPath(Path.Combine(resolvedRoot, relativePath));

        if (!combinedPath.StartsWith(resolvedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException($"Access Denied: Attempted path traversal outside the workspace boundary! Target: {relativePath}");
        }

        return combinedPath;
    }
}