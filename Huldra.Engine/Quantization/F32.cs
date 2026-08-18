using System.Runtime.InteropServices;
using Huldra.Engine.Tensors;

namespace Huldra.Engine.Quantization;

public readonly struct F32 : IQuantizationFormat<F32>
{
    public static TensorType TensorType => TensorType.F32;
    public static int BlockSize => 1;
    public static int BytesPerBlock => 4;
    public static bool IsQuantized => false;

    public static void DecodeBlock(ReadOnlySpan<byte> source, Span<float> destination)
    {
        if (source.Length != 4 || destination.Length < 1)
            throw new ArgumentException("F32 block requires 4 source bytes and one destination value.");
        destination[0] = MemoryMarshal.Read<float>(source);
    }
}
