using Huldra.Engine.Tensors;

namespace Huldra.Engine.Quantization;

public readonly struct F16 : IQuantizationFormat<F16>
{
    public static TensorType TensorType => TensorType.F16;
    public static int BlockSize => 1;
    public static int BytesPerBlock => 2;
    public static bool IsQuantized => false;

    public static void DecodeBlock(ReadOnlySpan<byte> source, Span<float> destination)
    {
        if (source.Length != 2 || destination.Length < 1)
            throw new ArgumentException("F16 block requires 2 source bytes and one destination value.");
        destination[0] = (float)BitConverter.ToHalf(source);
    }
}
