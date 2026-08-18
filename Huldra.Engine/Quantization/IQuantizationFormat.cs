using Huldra.Engine.Tensors;

namespace Huldra.Engine.Quantization;

/// <summary>
/// Compile-time description of a tensor storage format.
///
/// A format describes the bytes on disk; it does not know which CPU backend
/// will execute kernels over those bytes.
/// </summary>
public interface IQuantizationFormat<TSelf>
    where TSelf : IQuantizationFormat<TSelf>
{
    static abstract TensorType TensorType { get; }

    static abstract int BlockSize { get; }

    static abstract int BytesPerBlock { get; }

    static abstract bool IsQuantized { get; }

    static abstract void DecodeBlock(
        ReadOnlySpan<byte> source,
        Span<float> destination);
}
