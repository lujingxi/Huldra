using System.Runtime.InteropServices;
using Huldra.Engine.Tensors;

namespace Huldra.Engine.Quantization;

public readonly struct BF16 : IQuantizationFormat<BF16>
{
    public static TensorType TensorType => TensorType.Bf16;
    public static int BlockSize => 1;
    public static int BytesPerBlock => 2;
    public static bool IsQuantized => false;

    public static void DecodeBlock(ReadOnlySpan<byte> source, Span<float> destination)
    {
        if (source.Length != 2 || destination.Length < 1)
            throw new ArgumentException("BF16 block requires 2 source bytes and one destination value.");
        ushort bits = MemoryMarshal.Read<ushort>(source);
        destination[0] = BitConverter.Int32BitsToSingle((int)bits << 16);
    }
}
