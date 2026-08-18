using Huldra.Agent.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace Huldra.Cli;

internal class AgentTest
{
    public async static Task RunTestAsync()
    {
        Console.WriteLine("======================================");
        Console.WriteLine("        Huldra AI Agent CLI           ");
        Console.WriteLine("======================================");

        // 1. Initialize Dynamic Role Manager
        var roleManager = new RoleManager();
        Console.WriteLine("[Init] Setting up core role templates...");
        await roleManager.InitializeCoreRolesAsync();

        // 2. Setup Workspace
        var workspaceManager = new WorkspaceManager();
        Console.Write("Enter Workspace Name (e.g., ProjectA): ");
        var projectName = Console.ReadLine() ?? "DefaultProject";

        Console.Write("Enter Project Goal (e.g., Write a simple calculator in C#): ");
        var goal = Console.ReadLine() ?? "No goal defined";

        var context = await workspaceManager.InitializeWorkspaceAsync(projectName, goal);

        // 3. Connect to local LLM via llama.cpp or Ollama (defaulting to llama.cpp standard port)
        Console.WriteLine("\n[Init] Connecting to local LLM Service...");
        var llmService = new OpenAiCompatibleService("http://localhost:8080/v1", "local-model");

        // 4. Instantiate Orchestrator and run the autonomous loop
        var orchestrator = new AgentOrchestrator(context, roleManager, llmService);

        Console.WriteLine("\n[Ready] Huldra Agent is ready to work.");
        Console.WriteLine("Press Ctrl+C to stop the agent execution.");
        Console.WriteLine("Starting loop in 3 seconds...");
        await Task.Delay(3000);

        // Use a cancellation token source to handle manual shutdowns gracefully
        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (sender, eventArgs) =>
        {
            Console.WriteLine("\n[CLI] Shutdown signal received. Stopping Agent gracefully...");
            eventArgs.Cancel = true;
            cts.Cancel();
        };

        try
        {
            await orchestrator.RunLoopAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("[CLI] Agent successfully paused.");
        }

        Console.WriteLine("\nExecution finished. Check your workspace files under .huldra directory.");
        Console.ReadKey();
    }
}
