using System.Runtime.InteropServices;
using Huldra.Engine.Quantization;

namespace Huldra.Engine.Vector.Quantization;

public readonly struct Q4_0VectorKernel : IQuantizedKernel<Q4_0>
{
    public static float Dot(ReadOnlySpan<byte> weights, ReadOnlySpan<float> activation)
    {
        if (weights.Length != Q4_0.BytesPerBlock || activation.Length != Q4_0.BlockSize)
            throw new ArgumentException("Q4_0 dot product requires one complete block.");

        float d = (float)BitConverter.ToHalf(weights[..2]);
        ReadOnlySpan<byte> qs = weights.Slice(2, 16);

        // Keep the first implementation deliberately simple and allocation-free.
        // The format is now shared; only this backend-specific arithmetic can be
        // replaced by AVX2/AVX-512 kernels without touching Q4_0 itself.
        float sum = 0f;
        for (int i = 0; i < 16; i++)
        {
            sum += d * ((qs[i] & 0x0F) - 8) * activation[i];
            sum += d * ((qs[i] >> 4) - 8) * activation[i + 16];
        }
        return sum;
    }
}
