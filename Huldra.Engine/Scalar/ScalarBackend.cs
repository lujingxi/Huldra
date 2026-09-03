// Huldra-Verify: 0.6.1-3
using Huldra.Engine.Backends;
using Huldra.Engine.Quantization;
using Huldra.Engine.Tensors;

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Huldra.Engine.Scalar;

public sealed class ScalarBackend : IBackend
{
    private long _matMulCallCount;
    private long _matMulTotalWork;
    private long _matMulTotalElapsedTicks;

    private long[]? _matMulWorkerWork;
    private int _matMulWorkerCount;
    private int _matMulMaxConcurrentWorkers;

    public int Priority => 0;
    public string Name => "Scalar";
    public bool IsSupported => true;

    public ScalarBackend()
    {
        _matMulWorkerWork =
            new long[Environment.ProcessorCount];

        _matMulWorkerCount =
            _matMulWorkerWork.Length;
    }

    public void MatMul(Tensor a, Tensor b, Tensor result)
    {
        long startTimestamp = Stopwatch.GetTimestamp();

        var (In, Out, SeqLen) = BackendValidation.ValidateMatMul(a, b, result);

        int maxWorkerSlots = Environment.ProcessorCount;

        long[] localWorkerWork =
            new long[maxWorkerSlots];

        int localMaxConcurrentWorkers = 0;

        long workload = checked(
            (long)In *
            Out *
            SeqLen);

        Interlocked.Increment(ref _matMulCallCount);
        Interlocked.Add(ref _matMulTotalWork, workload);

        Memory<byte> aMemory;
        Memory<byte> bMemory;
        Memory<byte> resultMemory = result.Data;

        byte[]? aBuffer = null;
        byte[]? bBuffer = null;

        try
        {
            int aByteSize = checked(In * Out * sizeof(float));
            int bByteSize = checked(SeqLen * In * sizeof(float));

            // ------------------------------------------------------------
            // 1. Prepare weight matrix
            // ------------------------------------------------------------

            if (a.Type == TensorType.F32)
            {
                aMemory = a.Data;
            }
            else
            {
                aBuffer = ArrayPool<byte>.Shared.Rent(aByteSize);
                aMemory = aBuffer.AsMemory(0, aByteSize);

                QuantizationRuntime.Dequantize(
                    a.Type,
                    a.Data,
                    aMemory);
            }

            // ------------------------------------------------------------
            // 2. Prepare activation matrix
            // ------------------------------------------------------------

            if (b.Type == TensorType.F32)
            {
                bMemory = b.Data;
            }
            else
            {
                bBuffer = ArrayPool<byte>.Shared.Rent(bByteSize);
                bMemory = bBuffer.AsMemory(0, bByteSize);

                QuantizationRuntime.Dequantize(
                    b.Type,
                    b.Data,
                    bMemory);
            }

            // ------------------------------------------------------------
            // 3. MatMul
            //
            // IMPORTANT:
            // Keep the current workload decomposition unchanged.
            //
            // P0.6.1 at this stage is instrumentation only.
            // We are measuring why the current SeqLen-based decomposition
            // produces poor CPU utilisation during token-by-token decode.
            // ------------------------------------------------------------

            BackendParallel.For(
                SeqLen,
                1,
                (start, end, workerIndex) =>
                {
                    long workerWork = checked(
                        (long)(end - start) *
                        Out *
                        In);

                    localWorkerWork[workerIndex] += workerWork;

                        ReadOnlySpan<float> aSpan =
                        MemoryMarshal.Cast<byte, float>(
                            aMemory.Span);

                    ReadOnlySpan<float> bSpan =
                        MemoryMarshal.Cast<byte, float>(
                            bMemory.Span);

                    Span<float> resultSpan =
                        MemoryMarshal.Cast<byte, float>(
                            resultMemory.Span);

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

            for (int workerIndex = 0;
                 workerIndex < localWorkerWork.Length;
                 workerIndex++)
            {
                long workerWork = localWorkerWork[workerIndex];

                if (workerWork == 0)
                    continue;

                Interlocked.Add(
                    ref _matMulWorkerWork![workerIndex],
                    workerWork);
            }
        }
        finally
        {
            Interlocked.Add(
                ref _matMulTotalElapsedTicks,
                Stopwatch.GetTimestamp() - startTimestamp);

            if (aBuffer is not null)
                ArrayPool<byte>.Shared.Return(aBuffer);

            if (bBuffer is not null)
                ArrayPool<byte>.Shared.Return(bBuffer);
        }
    }

    public void RMSNorm(
        Tensor input,
        Tensor weight,
        Tensor output,
        float epsilon)
    {
        // Input/Output Shape: [SeqLen, EmbdLength]
        int seqLen = input.Shape[0];
        int embdLength = input.Shape[1];

        Memory<byte> weightMemory;
        byte[]? weightBuffer = null;

        try
        {
            if (weight.Type == TensorType.F32)
            {
                weightMemory = weight.Data;
            }
            else
            {
                weightBuffer =
                    ArrayPool<byte>.Shared.Rent(
                        checked(embdLength * sizeof(float)));

                weightMemory =
                    weightBuffer.AsMemory(
                        0,
                        embdLength * sizeof(float));

                QuantizationRuntime.Dequantize(
                    weight.Type,
                    weight.Data,
                    weightMemory);
            }

            Memory<byte> inputMemory = input.Data;
            Memory<byte> outputMemory = output.Data;

            Parallel.For(0, seqLen, i =>
            {
                ReadOnlySpan<float> inputSpan =
                    MemoryMarshal.Cast<byte, float>(inputMemory.Span);

                Span<float> outputSpan =
                    MemoryMarshal.Cast<byte, float>(outputMemory.Span);

                ReadOnlySpan<float> weightSpan =
                    MemoryMarshal.Cast<byte, float>(weightMemory.Span);

                int offset = i * embdLength;

                float sumSq = 0f;

                for (int j = 0; j < embdLength; j++)
                {
                    float val = inputSpan[offset + j];
                    sumSq += val * val;
                }

                float rms =
                    MathF.Sqrt(
                        sumSq / embdLength + epsilon);

                float invRms = 1.0f / rms;

                for (int j = 0; j < embdLength; j++)
                {
                    outputSpan[offset + j] =
                        inputSpan[offset + j] *
                        invRms *
                        weightSpan[j];
                }
            });
        }
        finally
        {
            if (weightBuffer is not null)
                ArrayPool<byte>.Shared.Return(weightBuffer);
        }
    }

    public void RoPE(
        Tensor q,
        Tensor k,
        int headCount,
        int headCountKv,
        int headDim,
        float ropeFreqBase,
        int startPos)
    {
        int seqLen = q.Shape[0];
        int qDim = q.Shape[1];
        int kvDim = k.Shape[1];

        Memory<byte> qMem = q.Data;
        Memory<byte> kMem = k.Data;

        Parallel.For(0, seqLen, i =>
        {
            int pos = startPos + i;

            Span<float> qSpan =
                MemoryMarshal.Cast<byte, float>(qMem.Span);

            Span<float> kSpan =
                MemoryMarshal.Cast<byte, float>(kMem.Span);

            for (int h = 0; h < headCount; h++)
            {
                int headOffset = h * headDim;
                int posOffset = i * qDim + headOffset;

                for (int d = 0; d < headDim / 2; d++)
                {
                    float theta =
                        pos *
                        MathF.Pow(
                            ropeFreqBase,
                            -2.0f * d / headDim);

                    float cosTheta = MathF.Cos(theta);
                    float sinTheta = MathF.Sin(theta);

                    float q0 =
                        qSpan[posOffset + d];

                    float q1 =
                        qSpan[
                            posOffset +
                            d +
                            headDim / 2];

                    qSpan[posOffset + d] =
                        q0 * cosTheta -
                        q1 * sinTheta;

                    qSpan[
                        posOffset +
                        d +
                        headDim / 2] =
                        q0 * sinTheta +
                        q1 * cosTheta;
                }
            }

            for (int h = 0; h < headCountKv; h++)
            {
                int headOffset = h * headDim;
                int posOffset = i * kvDim + headOffset;

                for (int d = 0; d < headDim / 2; d++)
                {
                    float theta =
                        pos *
                        MathF.Pow(
                            ropeFreqBase,
                            -2.0f * d / headDim);

                    float cosTheta = MathF.Cos(theta);
                    float sinTheta = MathF.Sin(theta);

                    float k0 =
                        kSpan[posOffset + d];

                    float k1 =
                        kSpan[
                            posOffset +
                            d +
                            headDim / 2];

                    kSpan[posOffset + d] =
                        k0 * cosTheta -
                        k1 * sinTheta;

                    kSpan[
                        posOffset +
                        d +
                        headDim / 2] =
                        k0 * sinTheta +
                        k1 * cosTheta;
                }
            }
        });
    }

    public void Attention(
        Tensor q,
        Tensor k,
        Tensor v,
        Tensor kCache,
        Tensor vCache,
        Tensor output,
        int headCount,
        int headCountKv,
        int headDim,
        int seqLen,
        int startPos)
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
            ReadOnlySpan<float> kSpan =
                MemoryMarshal.Cast<byte, float>(kMem.Span);

            ReadOnlySpan<float> vSpan =
                MemoryMarshal.Cast<byte, float>(vMem.Span);

            Span<float> kCacheSpan =
                MemoryMarshal.Cast<byte, float>(kCacheMem.Span);

            Span<float> vCacheSpan =
                MemoryMarshal.Cast<byte, float>(vCacheMem.Span);

            int cachePos = startPos + p;

            for (int h = 0; h < headCountKv; h++)
            {
                for (int d = 0; d < headDim; d++)
                {
                    int srcIdx =
                        p * kvDim +
                        h * headDim +
                        d;

                    int cacheIdx =
                        cachePos *
                        (headDim * headCountKv) +
                        h * headDim +
                        d;

                    kCacheSpan[cacheIdx] =
                        kSpan[srcIdx];

                    vCacheSpan[cacheIdx] =
                        vSpan[srcIdx];
                }
            }
        });

        // 2. Calculate Attention
        Parallel.For(0, seqLen, p =>
        {
            ReadOnlySpan<float> qSpan =
                MemoryMarshal.Cast<byte, float>(qMem.Span);

            ReadOnlySpan<float> kCacheSpan =
                MemoryMarshal.Cast<byte, float>(
                    kCacheMem.Span);

            ReadOnlySpan<float> vCacheSpan =
                MemoryMarshal.Cast<byte, float>(
                    vCacheMem.Span);

            Span<float> outSpan =
                MemoryMarshal.Cast<byte, float>(
                    outMem.Span);

            int currentPos = startPos + p;

            float[] scoreArr =
                ArrayPool<float>.Shared.Rent(
                    currentPos + 1);

            try
            {
                Span<float> scores =
                    scoreArr.AsSpan(
                        0,
                        currentPos + 1);

                for (int h = 0; h < headCount; h++)
                {
                    int kv_h =
                        h * headCountKv / headCount;

                    int qOffset =
                        p * qDim +
                        h * headDim;

                    // Calculate scores against all tokens
                    // up to currentPos.
                    for (int i = 0; i <= currentPos; i++)
                    {
                        int kOffset =
                            i *
                            (headDim * headCountKv) +
                            kv_h * headDim;

                        float sum = 0f;

                        for (int d = 0; d < headDim; d++)
                        {
                            sum +=
                                qSpan[qOffset + d] *
                                kCacheSpan[kOffset + d];
                        }

                        scores[i] =
                            sum /
                            MathF.Sqrt(headDim);
                    }

                    float maxVal = float.MinValue;

                    for (int i = 0; i <= currentPos; i++)
                    {
                        if (scores[i] > maxVal)
                            maxVal = scores[i];
                    }

                    float expSum = 0f;

                    for (int i = 0; i <= currentPos; i++)
                    {
                        scores[i] =
                            MathF.Exp(
                                scores[i] - maxVal);

                        expSum += scores[i];
                    }

                    for (int i = 0; i <= currentPos; i++)
                        scores[i] /= expSum;

                    int outOffset =
                        p * qDim +
                        h * headDim;

                    for (int d = 0; d < headDim; d++)
                    {
                        float sum = 0f;

                        for (int i = 0; i <= currentPos; i++)
                        {
                            int vOffset =
                                i *
                                (headDim * headCountKv) +
                                kv_h * headDim +
                                d;

                            sum +=
                                scores[i] *
                                vCacheSpan[vOffset];
                        }

                        outSpan[outOffset + d] =
                            sum;
                    }
                }
            }
            finally
            {
                ArrayPool<float>.Shared.Return(scoreArr);
            }
        });
    }

    public void SiLU(Tensor tensor)
    {
        Memory<byte> mem = tensor.Data;

        int len = mem.Length / sizeof(float);

        Parallel.For(0, len, i =>
        {
            Span<float> span =
                MemoryMarshal.Cast<byte, float>(
                    mem.Span);

            float x = span[i];

            span[i] =
                x /
                (1.0f + MathF.Exp(-x));
        });
    }

    public void Gelu(Tensor tensor)
    {
        Memory<byte> mem = tensor.Data;

        int len = mem.Length / sizeof(float);

        Parallel.For(0, len, i =>
        {
            Span<float> span =
                MemoryMarshal.Cast<byte, float>(
                    mem.Span);

            float x = span[i];
            float x3 = x * x * x;

            float inner =
                0.7978845608f *
                (x + 0.044715f * x3);

            span[i] =
                0.5f *
                x *
                (1.0f + MathF.Tanh(inner));
        });
    }

    public void Mul(
        Tensor a,
        Tensor b,
        Tensor result)
    {
        if (!a.Shape.SequenceEqual(b.Shape) ||
            !a.Shape.SequenceEqual(result.Shape))
        {
            throw new ArgumentException(
                "Tensors must have the same shape for element-wise multiplication.");
        }

        Memory<byte> aMem = a.Data;
        Memory<byte> bMem = b.Data;
        Memory<byte> resMem = result.Data;

        int len = aMem.Length / sizeof(float);

        Parallel.For(0, len, i =>
        {
            Span<float> aSpan =
                MemoryMarshal.Cast<byte, float>(
                    aMem.Span);

            Span<float> bSpan =
                MemoryMarshal.Cast<byte, float>(
                    bMem.Span);

            Span<float> resSpan =
                MemoryMarshal.Cast<byte, float>(
                    resMem.Span);

            resSpan[i] =
                aSpan[i] *
                bSpan[i];
        });
    }

    public void Add(
        Tensor a,
        Tensor b,
        Tensor result)
    {
        BackendValidation.ValidateElementwise(
            a,
            b,
            result,
            nameof(Add));

        Memory<byte> aMem = a.Data;
        Memory<byte> bMem = b.Data;
        Memory<byte> resMem = result.Data;

        int len = aMem.Length / sizeof(float);

        Parallel.For(0, len, i =>
        {
            Span<float> aSpan =
                MemoryMarshal.Cast<byte, float>(
                    aMem.Span);

            Span<float> bSpan =
                MemoryMarshal.Cast<byte, float>(
                    bMem.Span);

            Span<float> resSpan =
                MemoryMarshal.Cast<byte, float>(
                    resMem.Span);

            resSpan[i] =
                aSpan[i] +
                bSpan[i];
        });
    }

    public void AddBias(
        Tensor bias,
        Tensor tensor)
    {
        // tensor shape: [SeqLen, Dim]
        // bias shape: [Dim]

        int seqLen = tensor.Shape[0];
        int dim = tensor.Shape[1];

        Memory<byte> biasMemory;
        Memory<byte> tensorMemory = tensor.Data;

        byte[]? biasBuffer = null;

        try
        {
            if (bias.Type == TensorType.F32)
            {
                biasMemory = bias.Data;
            }
            else
            {
                biasBuffer =
                    ArrayPool<byte>.Shared.Rent(
                        checked(dim * sizeof(float)));

                biasMemory =
                    biasBuffer.AsMemory(
                        0,
                        dim * sizeof(float));

                QuantizationRuntime.Dequantize(
                    bias.Type,
                    bias.Data,
                    biasMemory);
            }

            Parallel.For(0, seqLen, seq =>
            {
                ReadOnlySpan<float> biasSpan =
                    MemoryMarshal.Cast<byte, float>(
                        biasMemory.Span);

                Span<float> tensorSpan =
                    MemoryMarshal.Cast<byte, float>(
                        tensorMemory.Span);

                int rowOffset = seq * dim;

                for (int d = 0; d < dim; d++)
                {
                    tensorSpan[rowOffset + d] +=
                        biasSpan[d];
                }
            });
        }
        finally
        {
            if (biasBuffer is not null)
                ArrayPool<byte>.Shared.Return(biasBuffer);
        }
    }

    public MatMulWorkloadSnapshot GetMatMulWorkloadSnapshot()
    {
        long[] workerWork =
            _matMulWorkerWork is null
                ? []
                : (long[])_matMulWorkerWork.Clone();

        return new MatMulWorkloadSnapshot(
            Volatile.Read(ref _matMulCallCount),
            Volatile.Read(ref _matMulTotalWork),
            Volatile.Read(ref _matMulTotalElapsedTicks),
            workerWork,
            Volatile.Read(ref _matMulWorkerCount));
    }

    public void ResetMatMulWorkloadStatistics()
    {
        Volatile.Write(
            ref _matMulCallCount,
            0);

        Volatile.Write(
            ref _matMulTotalWork,
            0);

        Volatile.Write(
            ref _matMulTotalElapsedTicks,
            0);

        if (_matMulWorkerWork is not null)
            Array.Clear(_matMulWorkerWork);
    }

    private void UpdateMaxConcurrentWorkers(int value)
    {
        while (true)
        {
            int current =
                Volatile.Read(
                    ref _matMulMaxConcurrentWorkers);

            if (value <= current)
                return;

            if (Interlocked.CompareExchange(
                    ref _matMulMaxConcurrentWorkers,
                    value,
                    current) == current)
            {
                return;
            }
        }
    }
}