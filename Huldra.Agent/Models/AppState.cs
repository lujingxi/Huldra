using System;
using System.Collections.Generic;
using System.Text;

namespace Huldra.Agent.Models;

/// <summary>
/// Implicit application state stored in state.json.
/// Managed automatically by the application.
/// </summary>
public class AppState
{
    /// <summary>
    /// The ID of the session that was active when the app was last closed.
    /// </summary>
    public string? LastActiveSessionId { get; set; }

    /// <summary>
    /// List of Session IDs that are pinned/visible in the left sidebar.
    /// Unpinned sessions still exist in the physical folders and can be found in the "History" menu.
    /// </summary>
    public List<string> PinnedSessionIds { get; set; } = [];

    // Future implicit UI states
    public bool IsSidebarExpanded { get; set; } = true;
}
