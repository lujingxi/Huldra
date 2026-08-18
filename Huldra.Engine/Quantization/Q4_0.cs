using Huldra.Engine.Tensors;

namespace Huldra.Engine.Quantization;

public readonly struct Q4_0 : IQuantizationFormat<Q4_0>
{
    public static TensorType TensorType => TensorType.Q4_0;
    public static int BlockSize => 32;
    public static int BytesPerBlock => 18;
    public static bool IsQuantized => true;

    public static void DecodeBlock(ReadOnlySpan<byte> source, Span<float> destination)
    {
        if (source.Length != BytesPerBlock || destination.Length < BlockSize)
            throw new ArgumentException("Q4_0 block requires 18 source bytes and 32 destination values.");

        float d = (float)BitConverter.ToHalf(source[..2]);
        ReadOnlySpan<byte> qs = source.Slice(2, 16);

        for (int i = 0; i < 16; i++)
        {
            destination[i] = d * ((qs[i] & 0x0F) - 8);
            destination[i + 16] = d * ((qs[i] >> 4) - 8);
        }
    }
}
