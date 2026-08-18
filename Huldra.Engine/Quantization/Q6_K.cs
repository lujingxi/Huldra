using System.Runtime.InteropServices;
using Huldra.Engine.Tensors;

namespace Huldra.Engine.Quantization;

public readonly struct Q6_K : IQuantizationFormat<Q6_K>
{
    public static TensorType TensorType => TensorType.Q6_K;
    public static int BlockSize => 256;
    public static int BytesPerBlock => 210;
    public static bool IsQuantized => true;

    public static void DecodeBlock(ReadOnlySpan<byte> source, Span<float> destination)
    {
        if (source.Length != BytesPerBlock || destination.Length < BlockSize)
            throw new ArgumentException("Q6_K block requires 210 source bytes and 256 destination values.");

        float d = (float)BitConverter.ToHalf(source.Slice(208, 2));
        ReadOnlySpan<byte> ql = source.Slice(0, 128);
        ReadOnlySpan<byte> qh = source.Slice(128, 64);
        ReadOnlySpan<sbyte> scales = MemoryMarshal.Cast<byte, sbyte>(source.Slice(192, 16));

        for (int j = 0; j < BlockSize; j++)
        {
            int low = (ql[j / 2] >> ((j & 1) * 4)) & 0x0F;
            int high = (qh[j / 4] >> ((j & 3) * 2)) & 0x03;
            int q = (low | (high << 4)) - 32;
            destination[j] = d * scales[j / 16] * q;
        }
    }
}
