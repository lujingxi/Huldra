using System.Diagnostics;
using System.Text.Json;
using Huldra.Agent.Models;

namespace Huldra.Agent.Services;

// Wrapper for safe polymorphic serialization
public class SessionMetaWrapper
{
    public string Type { get; set; } = "";
    public JsonElement Data { get; set; }
}

public class SessionManager
{
    private const string _sessionFileName = "session.json";
    private const string _chatFileName = "chat.json";
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    private readonly string _sessionsDir;

    public SessionManager(string? customBasePath = null)
    {
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _sessionsDir = customBasePath ?? Path.Combine(userProfile, ".huldra", "sessions");
        Directory.CreateDirectory(_sessionsDir);
    }

    public async Task SaveSessionAsync(ISession session)
    {
        var sessionDir = Path.Combine(_sessionsDir, session.Id);
        Directory.CreateDirectory(sessionDir);

        // 1. Save metadata to session.json
        var wrapper = new SessionMetaWrapper
        {
            Type = session is ChatSession ? "Chat" : "Workspace",
            Data = JsonSerializer.SerializeToElement(session, session.GetType(), _jsonOptions)
        };
        await File.WriteAllTextAsync(Path.Combine(sessionDir, _sessionFileName), JsonSerializer.Serialize(wrapper, _jsonOptions));

        // 2. Save chat history to chat.json
        await File.WriteAllTextAsync(Path.Combine(sessionDir, _chatFileName), JsonSerializer.Serialize(session.Messages, _jsonOptions));
    }

    public async Task<List<ISession>> LoadAllSessionsAsync()
    {
        var sessions = new List<ISession>();
        if (!Directory.Exists(_sessionsDir)) return sessions;

        foreach (var dir in Directory.GetDirectories(_sessionsDir))
        {
            var sessionFile = Path.Combine(dir, _sessionFileName);
            if (File.Exists(sessionFile))
            {
                try
                {
                    var wrapper = JsonSerializer.Deserialize<SessionMetaWrapper>(await File.ReadAllTextAsync(sessionFile), _jsonOptions);
                    if (wrapper != null)
                    {
                        ISession? session = wrapper.Type == "Chat"
                            ? JsonSerializer.Deserialize<ChatSession>(wrapper.Data.GetRawText(), _jsonOptions)
                            : JsonSerializer.Deserialize<WorkspaceSession>(wrapper.Data.GetRawText(), _jsonOptions);

                        if (session != null) sessions.Add(session);
                    }
                }
                catch { /* Ignore corrupted session files on startup */ }
            }
        }
        return sessions.OrderBy(s => s.CreatedAt).ToList();
    }

    public async Task<List<SessionMessage>> LoadChatAsync(string sessionId)
    {
        var chatFile = Path.Combine(_sessionsDir, sessionId, _chatFileName);
        if (File.Exists(chatFile))
        {
            try { return JsonSerializer.Deserialize<List<SessionMessage>>(await File.ReadAllTextAsync(chatFile), _jsonOptions) ?? []; }
            catch { return []; }
        }
        return [];
    }

    public async Task<WorkspaceContext> InitializeWorkspaceAsync(WorkspaceSession session)
    {
        var projectDir = Path.Combine(_sessionsDir, session.Id);
        var context = new WorkspaceContext { ProjectName = session.Name, RootDirectory = projectDir };

        if (!File.Exists(context.StatusFilePath))
        {
            Directory.CreateDirectory(context.RootDirectory);
            Directory.CreateDirectory(context.PlanDirectory);
            Directory.CreateDirectory(context.MemoryDirectory);
            Directory.CreateDirectory(context.InteractiveMemoryDirectory);
            Directory.CreateDirectory(context.KnowledgebaseDirectory);
            Directory.CreateDirectory(context.OutputDirectory);
            Directory.CreateDirectory(context.MemoDirectory);
            Directory.CreateDirectory(context.RolesDirectory);

            await File.WriteAllTextAsync(context.StatusFilePath, $"# Workspace Status & Progress\n\n## Project Overall Goal\n{session.ProjectGoal}\n\n## Current State\nInitialized and awaiting planning.\n");
            await File.WriteAllTextAsync(context.MainPlanFilePath, $"# Main Plan\n\n## Overall Goal\n{session.ProjectGoal}\n\n## Executable Phases\nWaiting for Planner to generate steps...\n");

            try
            {
                var pInfo = new ProcessStartInfo("git", "init") { WorkingDirectory = projectDir, CreateNoWindow = true, UseShellExecute = false };
                using var process = Process.Start(pInfo);
                if (process != null) await process.WaitForExitAsync();

                await ExecuteGitCommandAsync(projectDir, "add .");
                await ExecuteGitCommandAsync(projectDir, "commit -m \"Init\"");
            }
            catch { }
        }
        return context;
    }

    private async Task ExecuteGitCommandAsync(string workingDir, string arguments)
    {
        var pInfo = new ProcessStartInfo("git", arguments) { WorkingDirectory = workingDir, CreateNoWindow = true, UseShellExecute = false };
        using var process = Process.Start(pInfo);
        if (process != null) await process.WaitForExitAsync();
    }
}
