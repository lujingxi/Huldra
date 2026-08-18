using System.Reflection;
using Huldra.Agent.Models;

namespace Huldra.Agent.Services;

public class RoleManager
{
    private readonly string _globalRolesDir;
    private readonly Assembly _assembly;

    public RoleManager(string? customBasePath = null)
    {
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _globalRolesDir = customBasePath ?? Path.Combine(userProfile, ".huldra", "roles");
        Directory.CreateDirectory(_globalRolesDir);

        _assembly = Assembly.GetExecutingAssembly();
    }

    /// <summary>
    /// Automatically discovers and extracts ALL core roles from embedded resources 
    /// if they don't exist in the local user directory.
    /// </summary>
    public async Task InitializeCoreRolesAsync()
    {
        string resourcePrefix = "Huldra.Agent.Resources.DefaultRoles.";
        var allResourceNames = _assembly.GetManifestResourceNames();

        var roleResources = allResourceNames.Where(name =>
            name.StartsWith(resourcePrefix, StringComparison.OrdinalIgnoreCase) &&
            name.EndsWith(".md", StringComparison.OrdinalIgnoreCase));

        foreach (var resourceName in roleResources)
        {
            string fileName = resourceName.Substring(resourcePrefix.Length);
            var localFilePath = Path.Combine(_globalRolesDir, fileName);

            if (!File.Exists(localFilePath))
            {
                using var stream = _assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    using var reader = new StreamReader(stream);
                    var content = await reader.ReadToEndAsync();
                    await File.WriteAllTextAsync(localFilePath, content);
                    Console.WriteLine($"[RoleManager] Discovered and extracted default role: {fileName}");
                }
                else
                {
                    Console.WriteLine($"[RoleManager Warning] Could not read stream for resource: {resourceName}");
                }
            }
        }
    }

    public async Task<List<RoleDefinition>> LoadRolesAsync(string? workspaceRolesDir = null)
    {
        var roles = await LoadRolesFromDirectoryAsync(_globalRolesDir);

        if (!string.IsNullOrEmpty(workspaceRolesDir) && Directory.Exists(workspaceRolesDir))
        {
            var workspaceRoles = await LoadRolesFromDirectoryAsync(workspaceRolesDir);
            foreach (var wRole in workspaceRoles)
            {
                var existing = roles.FirstOrDefault(r => r.Name.Equals(wRole.Name, StringComparison.OrdinalIgnoreCase));
                if (existing != null) roles.Remove(existing);
                roles.Add(wRole);
            }
        }
        return roles;
    }

    private async Task<List<RoleDefinition>> LoadRolesFromDirectoryAsync(string directory)
    {
        var roles = new List<RoleDefinition>();
        if (!Directory.Exists(directory)) return roles;

        var files = Directory.GetFiles(directory, "*.md");
        foreach (var file in files)
        {
            var content = await File.ReadAllTextAsync(file);
            roles.Add(ParseRoleMarkdown(content, Path.GetFileNameWithoutExtension(file)));
        }
        return roles;
    }

    private RoleDefinition ParseRoleMarkdown(string content, string defaultName)
    {
        var name = ExtractSection(content, "# Name") ?? defaultName;
        var description = ExtractSection(content, "# Description") ?? "No description";
        var systemPrompt = ExtractSection(content, "# System Prompt") ?? "No prompt";
        var isCore = content.Contains("IsCoreRole: true");

        var toolsSection = ExtractSection(content, "# Allowed Tools");
        var allowedTools = new List<string>();
        if (!string.IsNullOrWhiteSpace(toolsSection))
        {
            allowedTools = toolsSection.Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                                       .Select(t => t.Trim())
                                       .Where(t => !string.IsNullOrEmpty(t))
                                       .ToList();
        }

        return new RoleDefinition
        {
            Name = name.Trim(),
            Description = description.Trim(),
            SystemPrompt = systemPrompt.Trim(),
            IsCoreRole = isCore,
            AllowedTools = allowedTools
        };
    }

    private string? ExtractSection(string content, string header)
    {
        var lines = content.Split('\n');
        var isReading = false;
        var result = new List<string>();

        foreach (var line in lines)
        {
            if (line.Trim().StartsWith("# "))
            {
                if (line.Trim().Equals(header, StringComparison.OrdinalIgnoreCase))
                {
                    isReading = true;
                    continue;
                }
                if (isReading) break;
            }
            else if (isReading)
            {
                result.Add(line);
            }
        }

        var text = string.Join("\n", result).Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }
}
