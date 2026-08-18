using Huldra.Engine.Quantization;

namespace Huldra.Engine.Tensors;

/// <summary>
/// Describes the physical storage layout of a GGML/GGUF tensor type.
///
/// Metadata is supplied by the runtime-discovered tensor format registry.
/// </summary>
public readonly record struct TensorTypeInfo(int BlockSize, int BytesPerBlock, bool IsQuantized)
{
    public static TensorTypeInfo For(TensorType type)
    {
        TensorFormatDescriptor descriptor = QuantizationRuntime.Registry.Get(type);

        return new TensorTypeInfo(
            descriptor.BlockSize,
            descriptor.BytesPerBlock,
            descriptor.IsQuantized);
    }

    public static long GetStorageSize(TensorType type, long elementCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(elementCount);

        TensorTypeInfo info = For(type);

        if (elementCount % info.BlockSize != 0)
        {
            throw new InvalidDataException($"Tensor type {type} requires element count to be a multiple of {info.BlockSize}, but got {elementCount}.");
        }

        return checked(
            (elementCount / info.BlockSize) *
            info.BytesPerBlock);
    }
}
