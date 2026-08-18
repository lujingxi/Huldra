using Huldra.Engine.Tensors;

namespace Huldra.Engine.Quantization;

internal delegate void TensorFormatDequantizeDelegate(
    ReadOnlyMemory<byte> source,
    Memory<byte> destination);

/// <summary>
/// Immutable runtime description of one tensor storage format.
///
/// Instances are created during startup discovery and reused afterwards.
/// </summary>
internal readonly record struct TensorFormatDescriptor(
    TensorType TensorType,
    int BlockSize,
    int BytesPerBlock,
    bool IsQuantized,
    TensorFormatDequantizeDelegate Dequantize);
