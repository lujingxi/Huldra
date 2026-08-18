using Huldra.Engine.Scalar;
using Huldra.Engine.Vector;
using System.Collections.Frozen;

namespace Huldra.Engine.Backends;

public sealed class BackendRuntime
{
    private static readonly Lazy<BackendRuntime> s_instance =
        new(static () => CreateDefault());

    public static BackendRuntime Instance => s_instance.Value;

    private readonly FrozenDictionary<string, BackendDescriptor> _backends;

    private BackendRuntime(
        FrozenDictionary<string, BackendDescriptor> backends)
    {
        _backends = backends;
    }

    private static BackendRuntime CreateDefault()
    {
        IReadOnlyCollection<BackendDescriptor> discovered =
            BackendDiscovery.Discover(
                AppDomain.CurrentDomain.GetAssemblies());

        var backends =
            new Dictionary<string, BackendDescriptor>(
                StringComparer.OrdinalIgnoreCase);

        foreach (BackendDescriptor descriptor in discovered)
        {
            if (!backends.TryAdd(descriptor.Name, descriptor))
            {
                throw new InvalidOperationException(
                    $"A backend named '{descriptor.Name}' " +
                    $"is already registered.");
            }
        }

        return new BackendRuntime(
            backends.ToFrozenDictionary(
                StringComparer.OrdinalIgnoreCase));
    }

    public IBackend GetBestBackend()
    {
        BackendDescriptor? selected = null;

        foreach (BackendDescriptor descriptor in _backends.Values)
        {
            if (!IsSupported(descriptor))
                continue;

            if (selected is null ||
                descriptor.Priority > selected.Priority)
            {
                selected = descriptor;
            }
        }

        if (selected is null)
        {
            throw new InvalidOperationException(
                "No supported backend is available.");
        }

        return selected.Instance;
    }

    public IBackend GetBackend(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!_backends.TryGetValue(
                name,
                out BackendDescriptor? descriptor))
        {
            throw new KeyNotFoundException(
                $"Backend '{name}' is not registered.");
        }

        if (!IsSupported(descriptor))
        {
            throw new NotSupportedException(
                $"Backend '{name}' is not supported on this system.");
        }

        return descriptor.Instance;
    }

    public IReadOnlyCollection<string> SupportedBackends
    {
        get
        {
            var result = new List<string>();

            foreach (BackendDescriptor descriptor in _backends.Values)
            {
                if (IsSupported(descriptor))
                    result.Add(descriptor.Name);
            }

            return result;
        }
    }

    private static bool IsSupported(
        BackendDescriptor descriptor)
    {
        return descriptor.Instance.IsSupported;
    }
}
