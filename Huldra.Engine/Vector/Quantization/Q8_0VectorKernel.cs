using System.Runtime.InteropServices;
using Huldra.Engine.Quantization;

namespace Huldra.Engine.Vector.Quantization;

public readonly struct Q8_0VectorKernel : IQuantizedKernel<Q8_0>
{
    public static float Dot(ReadOnlySpan<byte> weights, ReadOnlySpan<float> activation)
    {
        if (weights.Length != Q8_0.BytesPerBlock || activation.Length != Q8_0.BlockSize)
            throw new ArgumentException("Q8_0 dot product requires one complete block.");

        float d = (float)BitConverter.ToHalf(weights[..2]);
        ReadOnlySpan<sbyte> qs = MemoryMarshal.Cast<byte, sbyte>(weights.Slice(2, 32));

        float sum = 0f;
        for (int i = 0; i < Q8_0.BlockSize; i++)
            sum += d * qs[i] * activation[i];
        return sum;
    }
}
