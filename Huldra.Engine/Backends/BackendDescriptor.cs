namespace Huldra.Engine.Backends;

internal sealed record BackendDescriptor(
    Type BackendType,
    string Name,
    int Priority,
    IBackend Instance);
