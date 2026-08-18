using System.Reflection;

namespace Huldra.Engine.Quantization;

/// <summary>
/// Bridges a discovered System.Type to the generic static format contract.
///
/// Reflection is used only during startup discovery.
/// </summary>
internal static class TensorFormatDescriptorFactory
{
    private static readonly MethodInfo GenericCreateMethod =
        typeof(TensorFormatDescriptorFactory)
            .GetMethods(
                BindingFlags.Static |
                BindingFlags.NonPublic)
            .Single(
                method =>
                    method.Name == nameof(Create) &&
                    method.IsGenericMethodDefinition &&
                    method.GetGenericArguments().Length == 1 &&
                    method.GetParameters().Length == 0);

    internal static TensorFormatDescriptor Create(
        Type formatType)
    {
        ArgumentNullException.ThrowIfNull(formatType);

        MethodInfo closedMethod =
            GenericCreateMethod.MakeGenericMethod(formatType);

        Func<TensorFormatDescriptor> factory =
            closedMethod.CreateDelegate<
                Func<TensorFormatDescriptor>>();

        return factory();
    }

    private static TensorFormatDescriptor Create<TFormat>()
        where TFormat : IQuantizationFormat<TFormat>
    {
        return new TensorFormatDescriptor(
            TFormat.TensorType,
            TFormat.BlockSize,
            TFormat.BytesPerBlock,
            TFormat.IsQuantized,

            static (source, destination) =>
                QuantizationRuntime.Dequantize<TFormat>(
                    source,
                    destination));
    }
}
