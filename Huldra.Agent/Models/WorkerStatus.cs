namespace Huldra.Agent.Models;

/// <summary>
/// Lifecycle state of a Worker execution.
/// </summary>
public enum WorkerStatus
{
    /// <summary>
    /// The Worker has been created but has not started.
    /// </summary>
    Created,

    /// <summary>
    /// The Runtime is preparing the Worker for execution.
    /// </summary>
    Starting,

    /// <summary>
    /// The Worker is actively processing its task.
    /// </summary>
    Running,

    /// <summary>
    /// The Worker is suspended while waiting for an external event.
    /// </summary>
    Waiting,

    /// <summary>
    /// The Worker completed its task successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// The Worker terminated because of an execution failure.
    /// </summary>
    Failed,

    /// <summary>
    /// The Worker execution was cancelled.
    /// </summary>
    Cancelled
}