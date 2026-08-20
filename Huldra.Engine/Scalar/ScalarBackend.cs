using Huldra.Engine.Backends;
using Huldra.Engine.Quantization;
using Huldra.Engine.Tensors;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Huldra.Engine.Scalar;

public sealed class ScalarBackend : IBackend
{
    public int Priority => 0;
    public string Name => "Scalar";
    public bool IsSupported => true;

    public void MatMul(Tensor a, Tensor b, Tensor result)
    {
        var (In, Out, SeqLen) = BackendValidation.ValidateMatMul(a, b, result);

        // a (Weights): [InFeatures, OutFeatures] (Column-major from GGUF)
        // b (Hidden):  [SeqLen, InFeatures] (Row-major)
        // result:      [SeqLen, OutFeatures] (Row-major)

        // FIX: Correctly map shapes to In and Out features
        Memory<byte> aMemory;
        Memory<byte> bMemory;
        Memory<byte> resultMemory = result.Data;

        byte[]? aBuffer = null;
        byte[]? bBuffer = null;

        int aByteSize = In * Out * 4;
        int bByteSize = SeqLen * In * 4;

        if (a.Type == TensorType.F32)
        {
            aMemory = a.Data;
        }
        else
        {
            aBuffer = ArrayPool<byte>.Shared.Rent(aByteSize);
            aMemory = aBuffer.AsMemory(0, aByteSize);
            QuantizationRuntime.Dequantize(a.Type, a.Data, aMemory);
        }

        if (b.Type == TensorType.F32)
        {
            bMemory = b.Data;
        }
        else
        {
            bBuffer = ArrayPool<byte>.Shared.Rent(bByteSize);
            bMemory = bBuffer.AsMemory(0, bByteSize);
            QuantizationRuntime.Dequantize(b.Type, b.Data, bMemory);
        }

        // result[seq, o] = sum_i ( a[i, o] * b[seq, i] )
        BackendParallel.For(
            SeqLen,
            1,
            (start, end) =>
            {
                ReadOnlySpan<float> aSpan =
                    MemoryMarshal.Cast<byte, float>(aMemory.Span);

                ReadOnlySpan<float> bSpan =
                    MemoryMarshal.Cast<byte, float>(bMemory.Span);

                Span<float> resultSpan =
                    MemoryMarshal.Cast<byte, float>(resultMemory.Span);

                for (int seq = start; seq < end; seq++)
                {
                    int bRowOffset = seq * In;
                    int resRowOffset = seq * Out;

                    for (int o = 0; o < Out; o++)
                    {
                        float sum = 0f;

                        int aColOffset = o * In;

                        for (int i = 0; i < In; i++)
                        {
                            sum +=
                                aSpan[aColOffset + i] *
                                bSpan[bRowOffset + i];
                        }

                        resultSpan[resRowOffset + o] = sum;
                    }
                }
            });

        if (aBuffer is not null) ArrayPool<byte>.Shared.Return(aBuffer);
        if (bBuffer is not null) ArrayPool<byte>.Shared.Return(bBuffer);
    }

    public void RMSNorm(Tensor input, Tensor weight, Tensor output, float epsilon)
    {
        // Input/Output Shape: [SeqLen, EmbdLength]
        int seqLen = input.Shape[0];
        int embdLength = input.Shape[1];

        Memory<byte> weightMemory;
        byte[]? weightBuffer = null;

        if (weight.Type == TensorType.F32)
        {
            weightMemory = weight.Data;
        }
        else
        {
            weightBuffer = ArrayPool<byte>.Shared.Rent(embdLength * 4);
            weightMemory = weightBuffer.AsMemory(0, embdLength * 4);
            QuantizationRuntime.Dequantize(weight.Type, weight.Data, weightMemory);
        }

        Memory<byte> inputMemory = input.Data;
        Memory<byte> outputMemory = output.Data;

        Parallel.For(0, seqLen, i =>
        {
            ReadOnlySpan<float> inputSpan = MemoryMarshal.Cast<byte, float>(inputMemory.Span);
            Span<float> outputSpan = MemoryMarshal.Cast<byte, float>(outputMemory.Span);
            ReadOnlySpan<float> weightSpan = MemoryMarshal.Cast<byte, float>(weightMemory.Span);

            int offset = i * embdLength; // Row-major offset

            float sumSq = 0f;
            for (int j = 0; j < embdLength; j++)
            {
                float val = inputSpan[offset + j];
                sumSq += val * val;
            }

            float rms = MathF.Sqrt(sumSq / embdLength + epsilon);
            float invRms = 1.0f / rms;

            for (int j = 0; j < embdLength; j++)
            {
                outputSpan[offset + j] = inputSpan[offset + j] * invRms * weightSpan[j];
            }
        });

        if (weightBuffer is not null) ArrayPool<byte>.Shared.Return(weightBuffer);
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
            // FIX: Absolute position = startPos + i
            int pos = startPos + i;

            Span<float> qSpan = MemoryMarshal.Cast<byte, float>(qMem.Span);
            Span<float> kSpan = MemoryMarshal.Cast<byte, float>(kMem.Span);

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

        // 1. Update KV Cache
        Parallel.For(0, seqLen, p =>
        {
            ReadOnlySpan<float> kSpan = MemoryMarshal.Cast<byte, float>(kMem.Span);
            ReadOnlySpan<float> vSpan = MemoryMarshal.Cast<byte, float>(vMem.Span);
            Span<float> kCacheSpan = MemoryMarshal.Cast<byte, float>(kCacheMem.Span);
            Span<float> vCacheSpan = MemoryMarshal.Cast<byte, float>(vCacheMem.Span);

            // FIX: Write to cache at (startPos + p)
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

        // 2. Calculate Attention
        Parallel.For(0, seqLen, p =>
        {
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

                // FIX: Calculate scores against all tokens up to currentPos
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
        Memory<byte> mem = tensor.Data;
        int len = mem.Length / 4; // float is 4 bytes

        Parallel.For(0, len, i =>
        {
            Span<float> span = MemoryMarshal.Cast<byte, float>(mem.Span);
            float x = span[i];
            // SiLU(x) = x * sigmoid(x) = x / (1 + exp(-x))
            span[i] = x / (1.0f + MathF.Exp(-x));
        });
    }

    public void Gelu(Tensor tensor)
    {
        Memory<byte> mem = tensor.Data;
        int len = mem.Length / 4;

        Parallel.For(0, len, i =>
        {
            // Safely get span INSIDE the loop
            Span<float> span = MemoryMarshal.Cast<byte, float>(mem.Span);
            float x = span[i];
            float x3 = x * x * x;
            // Tanh approximation of GELU
            float inner = 0.7978845608f * (x + 0.044715f * x3);
            span[i] = 0.5f * x * (1.0f + MathF.Tanh(inner));
        });
    }

    public void Mul(Tensor a, Tensor b, Tensor result)
    {
        if (!a.Shape.SequenceEqual(b.Shape) || !a.Shape.SequenceEqual(result.Shape))
            throw new ArgumentException("Tensors must have the same shape for element-wise multiplication.");

        Memory<byte> aMem = a.Data;
        Memory<byte> bMem = b.Data;
        Memory<byte> resMem = result.Data;
        int len = aMem.Length / 4;

        Parallel.For(0, len, i =>
        {
            Span<float> aSpan = MemoryMarshal.Cast<byte, float>(aMem.Span);
            Span<float> bSpan = MemoryMarshal.Cast<byte, float>(bMem.Span);
            Span<float> resSpan = MemoryMarshal.Cast<byte, float>(resMem.Span);

            resSpan[i] = aSpan[i] * bSpan[i];
        });
    }

    public void Add(Tensor a, Tensor b, Tensor result)
    {
        BackendValidation.ValidateElementwise(a, b, result, nameof(Add));
        // Assuming all have the same shape [SeqLen, Dim]
        Memory<byte> aMem = a.Data;
        Memory<byte> bMem = b.Data;
        Memory<byte> resMem = result.Data;
        int len = aMem.Length / 4;

        Parallel.For(0, len, i =>
        {
            Span<float> aSpan = MemoryMarshal.Cast<byte, float>(aMem.Span);
            Span<float> bSpan = MemoryMarshal.Cast<byte, float>(bMem.Span);
            Span<float> resSpan = MemoryMarshal.Cast<byte, float>(resMem.Span);

            resSpan[i] = aSpan[i] + bSpan[i];
        });
    }

    public void AddBias(Tensor bias, Tensor tensor)
    {
        // tensor shape: [SeqLen, Dim]
        // bias shape: [Dim]
        int seqLen = tensor.Shape[0];
        int dim = tensor.Shape[1];

        Memory<byte> biasMemory;
        Memory<byte> tensorMemory = tensor.Data;

        byte[]? biasBuffer = null;
        if (bias.Type == TensorType.F32)
        {
            biasMemory = bias.Data;
        }
        else
        {
            biasBuffer = ArrayPool<byte>.Shared.Rent(dim * 4);
            biasMemory = biasBuffer.AsMemory(0, dim * 4);
            QuantizationRuntime.Dequantize(bias.Type, bias.Data, biasMemory);
        }

        Parallel.For(0, seqLen, seq =>
        {
            ReadOnlySpan<float> biasSpan = MemoryMarshal.Cast<byte, float>(biasMemory.Span);
            Span<float> tensorSpan = MemoryMarshal.Cast<byte, float>(tensorMemory.Span);

            int rowOffset = seq * dim;
            for (int d = 0; d < dim; d++)
            {
                tensorSpan[rowOffset + d] += biasSpan[d];
            }
        });

        if (biasBuffer is not null) ArrayPool<byte>.Shared.Return(biasBuffer);
    }
}
