namespace Huldra.Engine.Quantization;

/// <summary>
/// Backend-specific execution contract for a quantized format.
/// Static members keep the hot path free of interface-object dispatch.
/// </summary>
public interface IQuantizedKernel<TFormat>
    where TFormat : IQuantizationFormat<TFormat>
{
    static abstract float Dot(
        ReadOnlySpan<byte> weights,
        ReadOnlySpan<float> activation);
}
