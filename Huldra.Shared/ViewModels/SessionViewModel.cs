using Huldra.Agent.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Huldra.Shared.ViewModels;

public abstract class SessionViewModel
{
    public ISession Model { get; }
    public string BackgroundColor { get; set; } = "#005FB8";
    public bool IsExecuting { get; set; } = false;
    public bool IsChatLoaded { get; set; } = false;

    public List<SessionMessage> Messages => Model.Messages;

    protected SessionViewModel(ISession model)
    {
        Model = model;
    }
}
