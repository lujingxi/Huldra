using System.Runtime.InteropServices;
using Huldra.Engine.Tensors;

namespace Huldra.Engine.Quantization;

public readonly struct Q8_0 : IQuantizationFormat<Q8_0>
{
    public static TensorType TensorType => TensorType.Q8_0;
    public static int BlockSize => 32;
    public static int BytesPerBlock => 34;
    public static bool IsQuantized => true;

    public static void DecodeBlock(ReadOnlySpan<byte> source, Span<float> destination)
    {
        if (source.Length != BytesPerBlock || destination.Length < BlockSize)
            throw new ArgumentException("Q8_0 block requires 34 source bytes and 32 destination values.");

        float d = (float)BitConverter.ToHalf(source[..2]);
        ReadOnlySpan<sbyte> qs = MemoryMarshal.Cast<byte, sbyte>(source.Slice(2, 32));
        for (int i = 0; i < BlockSize; i++)
            destination[i] = d * qs[i];
    }
}
