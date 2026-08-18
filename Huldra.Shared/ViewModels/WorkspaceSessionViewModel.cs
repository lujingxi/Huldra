using Huldra.Agent.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Huldra.Shared.ViewModels;

public class WorkspaceSessionViewModel : SessionViewModel
{
    public WorkspaceSession WorkspaceModel => (WorkspaceSession)Model;

    public string PlanText { get; set; } = "";
    public string WorkspaceStatusText { get; set; } = "Ready to initialize workspace.";
    public string LogConsole { get; set; } = "System ready.\n";
    public bool IsPreparationPhase { get; set; } = true;

    public WorkspaceSessionViewModel(WorkspaceSession model) : base(model) { }
}
