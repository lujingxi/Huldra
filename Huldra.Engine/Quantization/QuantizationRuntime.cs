using Huldra.Engine.Tensors;

namespace Huldra.Engine.Quantization;

/// <summary>
/// Runtime boundary between GGUF's dynamic TensorType and
/// compile-time format code.
///
/// Format discovery happens once. Normal inference uses only
/// cached delegates and a FrozenDictionary lookup.
/// </summary>
public static class QuantizationRuntime
{
    private static readonly Lazy<TensorFormatRegistry> _registry =
        new(TensorFormatRegistry.CreateDefault, LazyThreadSafetyMode.ExecutionAndPublication);

    internal static TensorFormatRegistry Registry => _registry.Value;

    public static void Dequantize(TensorType type, ReadOnlyMemory<byte> source, Memory<byte> destination)
    {
        TensorFormatDescriptor format = Registry.Get(type);
        format.Dequantize(source, destination);
    }

    public static void Dequantize<TFormat>(ReadOnlyMemory<byte> source, Memory<byte> destination)
        where TFormat : IQuantizationFormat<TFormat>
    {
        int bytesPerBlock = TFormat.BytesPerBlock;
        int blockSize = TFormat.BlockSize;

        if (bytesPerBlock <= 0)
        {
            throw new InvalidOperationException($"{TFormat.TensorType} has an invalid byte block size {bytesPerBlock}.");
        }

        if (blockSize <= 0)
        {
            throw new InvalidOperationException($"{TFormat.TensorType} has an invalid element block size {blockSize}.");
        }

        if (source.Length % bytesPerBlock != 0)
        {
            throw new InvalidDataException(
                $"{TFormat.TensorType} source length " +
                $"{source.Length} is not divisible by " +
                $"block size in bytes {bytesPerBlock}.");
        }

        int blockCount = source.Length / bytesPerBlock;
        int elementCount = checked(blockCount * blockSize);
        int requiredDestinationBytes = checked(elementCount * sizeof(float));

        if (destination.Length < requiredDestinationBytes)
        {
            throw new ArgumentException($"Destination is too small for {elementCount} F32 values.", nameof(destination));
        }

        ReadOnlySpan<byte> src = source.Span;
        Span<float> dst = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, float>(destination.Span);

        for (int block = 0; block < blockCount; block++)
        {
            int srcOffset = checked(block * bytesPerBlock);
            int dstOffset = checked(block * blockSize);

            TFormat.DecodeBlock(
                src.Slice(srcOffset, bytesPerBlock),
                dst.Slice(dstOffset, blockSize));
        }
    }
}
