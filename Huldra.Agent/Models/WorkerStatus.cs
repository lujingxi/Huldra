namespace Huldra.Agent.Models;

public enum WorkerStatus
{
    Created,
    Starting,
    Running,
    Waiting,
    Completed,
    Failed,
    Cancelled,
    Stopped
}
