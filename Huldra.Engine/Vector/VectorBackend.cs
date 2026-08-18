using Huldra.Engine.Backends;
using Huldra.Engine.Quantization;
using Huldra.Engine.Vector.Quantization;
using Huldra.Engine.Tensors;
using System.Buffers;
using System.Collections.Concurrent;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Huldra.Engine.Vector;

public sealed class VectorBackend : IBackend
{
    public int Priority => 100;
    public string Name => "Vector";
    public bool IsSupported => true;

    public void MatMul(Tensor a, Tensor b, Tensor result)
    {
        var (inputSize, outputSize, sequenceLength) =
            BackendValidation.ValidateMatMul(a, b, result);

        // Fast path: quantized weights x F32 activations.
        // Runtime TensorType dispatch happens once at this boundary; the hot
        // block loop is then compiled against a concrete format/kernel pair.
        if (!a.IsF32 && b.IsF32 && VectorQuantizedKernelRuntime.TryMatMul(a, b, result, inputSize, outputSize, sequenceLength))
            return;

        // Reference/fallback path. It is intentionally kept simple until
        // every quantized format has a verified direct-dot implementation.
        Memory<byte> aMemory = a.Data;
        Memory<byte> bMemory = b.Data;
        Memory<byte> resultMemory = result.Data;

        byte[]? aBuffer = null;
        byte[]? bBuffer = null;

        try
        {
            if (!a.IsF32)
            {
                int aByteSize = checked(inputSize * outputSize * sizeof(float));
                aBuffer = ArrayPool<byte>.Shared.Rent(aByteSize);
                aMemory = aBuffer.AsMemory(0, aByteSize);
                QuantizationRuntime.Dequantize(a.Type, a.Data, aMemory);
            }

            if (!b.IsF32)
            {
                int bByteSize = checked(sequenceLength * inputSize * sizeof(float));
                bBuffer = ArrayPool<byte>.Shared.Rent(bByteSize);
                bMemory = bBuffer.AsMemory(0, bByteSize);
                QuantizationRuntime.Dequantize(b.Type, b.Data, bMemory);
            }

            BackendParallel.For(outputSize, 8, (start, end) =>
            {
                ReadOnlySpan<float> aSpan = MemoryMarshal.Cast<byte, float>(aMemory.Span);
                ReadOnlySpan<float> bSpan = MemoryMarshal.Cast<byte, float>(bMemory.Span);
                Span<float> resultSpan = MemoryMarshal.Cast<byte, float>(resultMemory.Span);

                for (int o = start; o < end; o++)
                {
                    ReadOnlySpan<float> aColumn = aSpan.Slice(o * inputSize, inputSize);
                    for (int seq = 0; seq < sequenceLength; seq++)
                    {
                        ReadOnlySpan<float> bRow = bSpan.Slice(seq * inputSize, inputSize);
                        Vector<float> sumVec = Vector<float>.Zero;
                        int i = 0;

                        for (; i <= inputSize - Vector<float>.Count; i += Vector<float>.Count)
                        {
                            sumVec +=
                                new Vector<float>(aColumn.Slice(i, Vector<float>.Count)) *
                                new Vector<float>(bRow.Slice(i, Vector<float>.Count));
                        }

                        float sum = System.Numerics.Vector.Dot(sumVec, Vector<float>.One);
                        for (; i < inputSize; i++)
                            sum += aColumn[i] * bRow[i];

                        resultSpan[seq * outputSize + o] = sum;
                    }
                }
            });
        }
        finally
        {
            if (aBuffer is not null)
                ArrayPool<byte>.Shared.Return(aBuffer);
            if (bBuffer is not null)
                ArrayPool<byte>.Shared.Return(bBuffer);
        }
    }

    public void RMSNorm(Tensor input, Tensor weight, Tensor output, float epsilon)
    {
        input.ValidateStorage();
        weight.ValidateStorage();
        output.ValidateStorage();

        if (!input.IsF32 || !output.IsF32)
            throw new NotSupportedException("RMSNorm currently requires F32 input and output tensors.");

        if (input.Shape.Length != 2 ||
            !output.Shape.SequenceEqual(input.Shape) ||
            weight.ElementCount != input.Shape[1])
            throw new ArgumentException("RMSNorm tensor shapes are incompatible.");

        int seqLen = input.Shape[0];
        int embdLength = input.Shape[1];

        Memory<byte> weightMemory = weight.Data;
        byte[]? weightBuffer = null;

        try
        {
            if (!weight.IsF32)
            {
                int byteSize = checked(embdLength * sizeof(float));
                weightBuffer = ArrayPool<byte>.Shared.Rent(byteSize);
                weightMemory = weightBuffer.AsMemory(0, byteSize);
                QuantizationRuntime.Dequantize(weight.Type, weight.Data, weightMemory);
            }

            BackendParallel.For(seqLen, 1, (start, end) =>
            {
                ReadOnlySpan<float> weightSpan = MemoryMarshal.Cast<byte, float>(weightMemory.Span);
                ReadOnlySpan<float> inputSpan = input.AsFloatSpan();
                Span<float> outputSpan = output.AsFloatSpan();

                for (int row = start; row < end; row++)
                {
                    int offset = row * embdLength;
                    Vector<float> sumSqVec = Vector<float>.Zero;
                    int j = 0;

                    for (; j <= embdLength - Vector<float>.Count; j += Vector<float>.Count)
                    {
                        Vector<float> x = new(inputSpan.Slice(offset + j, Vector<float>.Count));
                        sumSqVec += x * x;
                    }

                    float sumSq = System.Numerics.Vector.Dot(sumSqVec, Vector<float>.One);
                    for (; j < embdLength; j++)
                    {
                        float x = inputSpan[offset + j];
                        sumSq += x * x;
                    }

                    float invRms = 1f / MathF.Sqrt(sumSq / embdLength + epsilon);
                    Vector<float> inv = new(invRms);

                    j = 0;
                    for (; j <= embdLength - Vector<float>.Count; j += Vector<float>.Count)
                    {
                        Vector<float> x = new(inputSpan.Slice(offset + j, Vector<float>.Count));
                        Vector<float> w = new(weightSpan.Slice(j, Vector<float>.Count));
                        (x * inv * w).CopyTo(outputSpan.Slice(offset + j, Vector<float>.Count));
                    }

                    for (; j < embdLength; j++)
                        outputSpan[offset + j] = inputSpan[offset + j] * invRms * weightSpan[j];
                }
            });
        }
        finally
        {
            if (weightBuffer is not null)
                ArrayPool<byte>.Shared.Return(weightBuffer);
        }
    }

    public void RoPE(Tensor q, Tensor k, int headCount, int headCountKv, int headDim, float ropeFreqBase, int startPos)
    {
        int seqLen = q.Shape[0];
        int qDim = q.Shape[1];
        int kvDim = k.Shape[1];

        Memory<byte> qMem = q.Data;
        Memory<byte> kMem = k.Data;

        Parallel.For(0, seqLen, i =>
        {
            // CRITICAL FIX: Get spans INSIDE the loop
            Span<float> qSpan = MemoryMarshal.Cast<byte, float>(qMem.Span);
            Span<float> kSpan = MemoryMarshal.Cast<byte, float>(kMem.Span);

            int pos = startPos + i;

            for (int h = 0; h < headCount; h++)
            {
                int headOffset = h * headDim;
                int posOffset = i * qDim + headOffset;

                for (int d = 0; d < headDim / 2; d++)
                {
                    float theta = pos * MathF.Pow(ropeFreqBase, -2.0f * d / headDim);
                    float cosTheta = MathF.Cos(theta);
                    float sinTheta = MathF.Sin(theta);

                    float q0 = qSpan[posOffset + d];
                    float q1 = qSpan[posOffset + d + headDim / 2];

                    qSpan[posOffset + d] = q0 * cosTheta - q1 * sinTheta;
                    qSpan[posOffset + d + headDim / 2] = q0 * sinTheta + q1 * cosTheta;
                }
            }

            for (int h = 0; h < headCountKv; h++)
            {
                int headOffset = h * headDim;
                int posOffset = i * kvDim + headOffset;

                for (int d = 0; d < headDim / 2; d++)
                {
                    float theta = pos * MathF.Pow(ropeFreqBase, -2.0f * d / headDim);
                    float cosTheta = MathF.Cos(theta);
                    float sinTheta = MathF.Sin(theta);

                    float k0 = kSpan[posOffset + d];
                    float k1 = kSpan[posOffset + d + headDim / 2];

                    kSpan[posOffset + d] = k0 * cosTheta - k1 * sinTheta;
                    kSpan[posOffset + d + headDim / 2] = k0 * sinTheta + k1 * cosTheta;
                }
            }
        });
    }

    public void Attention(Tensor q, Tensor k, Tensor v, Tensor kCache, Tensor vCache, Tensor output, int headCount, int headCountKv, int headDim, int seqLen, int startPos)
    {
        int qDim = q.Shape[1];
        int kvDim = headCountKv * headDim;

        Memory<byte> qMem = q.Data;
        Memory<byte> kMem = k.Data;
        Memory<byte> vMem = v.Data;
        Memory<byte> kCacheMem = kCache.Data;
        Memory<byte> vCacheMem = vCache.Data;
        Memory<byte> outMem = output.Data;

        Parallel.For(0, seqLen, p =>
        {
            // CRITICAL FIX: Get spans INSIDE the loop
            ReadOnlySpan<float> kSpan = MemoryMarshal.Cast<byte, float>(kMem.Span);
            ReadOnlySpan<float> vSpan = MemoryMarshal.Cast<byte, float>(vMem.Span);
            Span<float> kCacheSpan = MemoryMarshal.Cast<byte, float>(kCacheMem.Span);
            Span<float> vCacheSpan = MemoryMarshal.Cast<byte, float>(vCacheMem.Span);

            int cachePos = startPos + p;
            for (int h = 0; h < headCountKv; h++)
            {
                for (int d = 0; d < headDim; d++)
                {
                    int srcIdx = p * kvDim + h * headDim + d;
                    int cacheIdx = cachePos * (headDim * headCountKv) + h * headDim + d;
                    kCacheSpan[cacheIdx] = kSpan[srcIdx];
                    vCacheSpan[cacheIdx] = vSpan[srcIdx];
                }
            }
        });

        Parallel.For(0, seqLen, p =>
        {
            // CRITICAL FIX: Get spans INSIDE the loop
            ReadOnlySpan<float> qSpan = MemoryMarshal.Cast<byte, float>(qMem.Span);
            ReadOnlySpan<float> kCacheSpan = MemoryMarshal.Cast<byte, float>(kCacheMem.Span);
            ReadOnlySpan<float> vCacheSpan = MemoryMarshal.Cast<byte, float>(vCacheMem.Span);
            Span<float> outSpan = MemoryMarshal.Cast<byte, float>(outMem.Span);

            int currentPos = startPos + p;
            float[] scoreArr = ArrayPool<float>.Shared.Rent(currentPos + 1);
            Span<float> scores = scoreArr.AsSpan(0, currentPos + 1);

            for (int h = 0; h < headCount; h++)
            {
                int kv_h = h * headCountKv / headCount;
                int qOffset = p * qDim + h * headDim;

                for (int i = 0; i <= currentPos; i++)
                {
                    int kOffset = i * (headDim * headCountKv) + kv_h * headDim;
                    float sum = 0f;
                    for (int d = 0; d < headDim; d++)
                    {
                        sum += qSpan[qOffset + d] * kCacheSpan[kOffset + d];
                    }
                    scores[i] = sum / MathF.Sqrt(headDim);
                }

                float maxVal = float.MinValue;
                for (int i = 0; i <= currentPos; i++) if (scores[i] > maxVal) maxVal = scores[i];

                float expSum = 0f;
                for (int i = 0; i <= currentPos; i++)
                {
                    scores[i] = MathF.Exp(scores[i] - maxVal);
                    expSum += scores[i];
                }
                for (int i = 0; i <= currentPos; i++) scores[i] /= expSum;

                int outOffset = p * qDim + h * headDim;
                for (int d = 0; d < headDim; d++)
                {
                    float sum = 0f;
                    for (int i = 0; i <= currentPos; i++)
                    {
                        int vOffset = i * (headDim * headCountKv) + kv_h * headDim + d;
                        sum += scores[i] * vCacheSpan[vOffset];
                    }
                    outSpan[outOffset + d] = sum;
                }
            }
            ArrayPool<float>.Shared.Return(scoreArr);
        });
    }

    public void SiLU(Tensor tensor)
    {
        tensor.ValidateStorage();
        if (!tensor.IsF32)
            throw new NotSupportedException("SiLU currently requires an F32 tensor.");

        Span<float> span = tensor.AsFloatSpan();
        for (int i = 0; i < span.Length; i++)
        {
            float x = span[i];
            span[i] = x / (1.0f + MathF.Exp(-x));
        }
    }

    public void Gelu(Tensor tensor)
    {
        tensor.ValidateStorage();
        if (!tensor.IsF32)
            throw new NotSupportedException("GELU currently requires an F32 tensor.");

        Span<float> span = tensor.AsFloatSpan();
        for (int i = 0; i < span.Length; i++)
        {
            float x = span[i];
            float x3 = x * x * x;
            float inner = 0.7978845608f * (x + 0.044715f * x3);
            span[i] = 0.5f * x * (1.0f + MathF.Tanh(inner));
        }
    }

    public void Mul(Tensor a, Tensor b, Tensor result)
    {
        BackendValidation.ValidateElementwise(a, b, result, nameof(Mul));

        ReadOnlySpan<float> aSpan = a.AsFloatSpan();
        ReadOnlySpan<float> bSpan = b.AsFloatSpan();
        Span<float> resultSpan = result.AsFloatSpan();
        int length = resultSpan.Length;
        int width = Vector<float>.Count;

        for (int i = 0; i <= length - width; i += width)
        {
            (new Vector<float>(aSpan.Slice(i, width)) *
             new Vector<float>(bSpan.Slice(i, width)))
                .CopyTo(resultSpan.Slice(i, width));
        }

        for (int i = length - (length % width); i < length; i++)
            resultSpan[i] = aSpan[i] * bSpan[i];
    }

    public void Add(Tensor a, Tensor b, Tensor result)
    {
        BackendValidation.ValidateElementwise(a, b, result, nameof(Add));

        ReadOnlySpan<float> aSpan = a.AsFloatSpan();
        ReadOnlySpan<float> bSpan = b.AsFloatSpan();
        Span<float> resultSpan = result.AsFloatSpan();
        int length = resultSpan.Length;
        int width = Vector<float>.Count;

        for (int i = 0; i <= length - width; i += width)
        {
            (new Vector<float>(aSpan.Slice(i, width)) +
             new Vector<float>(bSpan.Slice(i, width)))
                .CopyTo(resultSpan.Slice(i, width));
        }

        for (int i = length - (length % width); i < length; i++)
            resultSpan[i] = aSpan[i] + bSpan[i];
    }

    public void AddBias(Tensor bias, Tensor tensor)
    {
        bias.ValidateStorage();
        tensor.ValidateStorage();

        if (!tensor.IsF32 || bias.ElementCount != tensor.Shape[^1])
            throw new NotSupportedException("AddBias currently requires an F32 destination and a bias matching the final dimension.");

        int seqLen = tensor.Shape[0];
        int dim = tensor.Shape[1];

        Memory<byte> biasMemory = bias.Data;
        byte[]? biasBuffer = null;

        try
        {
            if (!bias.IsF32)
            {
                int byteSize = checked(dim * sizeof(float));
                biasBuffer = ArrayPool<byte>.Shared.Rent(byteSize);
                biasMemory = biasBuffer.AsMemory(0, byteSize);
                QuantizationRuntime.Dequantize(bias.Type, bias.Data, biasMemory);
            }

            int width = Vector<float>.Count;

            BackendParallel.For(seqLen, 1, (start, end) =>
            {
                ReadOnlySpan<float> biasSpan = MemoryMarshal.Cast<byte, float>(biasMemory.Span);
                Span<float> tensorSpan = tensor.AsFloatSpan();

                for (int seq = start; seq < end; seq++)
                {
                    int rowOffset = seq * dim;
                    int i = 0;
                    for (; i <= dim - width; i += width)
                    {
                        (new Vector<float>(tensorSpan.Slice(rowOffset + i, width)) +
                         new Vector<float>(biasSpan.Slice(i, width)))
                            .CopyTo(tensorSpan.Slice(rowOffset + i, width));
                    }

                    for (; i < dim; i++)
                        tensorSpan[rowOffset + i] += biasSpan[i];
                }
            });
        }
        finally
        {
            if (biasBuffer is not null)
                ArrayPool<byte>.Shared.Return(biasBuffer);
        }
    }
}
