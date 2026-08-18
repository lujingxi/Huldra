using System.Collections.Frozen;
using System.Reflection;
using Huldra.Engine.Tensors;

namespace Huldra.Engine.Quantization;

/// <summary>
/// Discovers tensor storage formats once and exposes a read-only lookup
/// for the rest of the engine.
/// </summary>
internal sealed class TensorFormatRegistry
{
    private readonly FrozenDictionary<TensorType, TensorFormatDescriptor> _formats;

    private TensorFormatRegistry(FrozenDictionary<TensorType, TensorFormatDescriptor> formats)
    {
        _formats = formats;
    }

    internal static TensorFormatRegistry CreateDefault() => Create(AppDomain.CurrentDomain.GetAssemblies());

    internal static TensorFormatRegistry Create(IEnumerable<Assembly> assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        var formats = new Dictionary<TensorType, TensorFormatDescriptor>();

        foreach (Type formatType in
         TensorFormatDiscovery.Discover(assemblies))
        {
            TensorFormatDescriptor descriptor = TensorFormatDescriptorFactory.Create(formatType);
            TensorFormatValidator.Validate(descriptor, formatType);

            if (!formats.TryAdd(descriptor.TensorType, descriptor))
            {
                throw new InvalidOperationException(
                    $"Duplicate tensor storage format " +
                    $"'{descriptor.TensorType}'. " +
                    $"Implementation: {formatType.FullName}.");
            }
        }

        return new TensorFormatRegistry(formats.ToFrozenDictionary());
    }

    internal bool TryGet(TensorType type, out TensorFormatDescriptor descriptor)
        => _formats.TryGetValue(type, out descriptor);

    internal TensorFormatDescriptor Get(TensorType type)
    {
        if (_formats.TryGetValue(type, out TensorFormatDescriptor descriptor))
        {
            return descriptor;
        }

        throw new NotSupportedException(
            $"Tensor type {type} is not supported by " +
            $"the tensor storage format registry.");
    }

    internal IReadOnlyCollection<TensorType> SupportedTypes => _formats.Keys;
}
