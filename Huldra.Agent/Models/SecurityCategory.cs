namespace Huldra.Agent.Models;

/// <summary>
/// Stable identifiers for security policy categories.
/// The model intentionally remains extensible: callers may define additional
/// category identifiers without changing this type.
/// </summary>
public static class SecurityCategory
{
    public const string FileAccess = "file.access";
    public const string FileModification = "file.modification";
    public const string FileDeletion = "file.deletion";
    public const string TerminalExecution = "terminal.execution";
    public const string NetworkAccess = "network.access";
    public const string ExternalDataTransmission = "data.external-transmission";
    public const string SensitiveDataAccess = "data.sensitive";
    public const string WorkerControl = "worker.control";
}
