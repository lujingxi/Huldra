using Huldra.Engine.Quantization;
using Huldra.Engine.Tensors;
using Huldra.Engine.Backends;
using System.Runtime.InteropServices;

namespace Huldra.Engine.Vector.Quantization;

internal static class VectorQuantizedKernelRuntime
{
    public static bool TryMatMul(
        Tensor weights,
        Tensor activation,
        Tensor result,
        int inputSize,
        int outputSize,
        int sequenceLength)
        => weights.Type switch
        {
            TensorType.Q4_0 => MatMul<Q4_0, Q4_0VectorKernel>(weights, activation, result, inputSize, outputSize, sequenceLength),
            TensorType.Q8_0 => MatMul<Q8_0, Q8_0VectorKernel>(weights, activation, result, inputSize, outputSize, sequenceLength),
            _ => false
        };

    private static bool MatMul<TFormat, TKernel>(
        Tensor weights,
        Tensor activation,
        Tensor result,
        int inputSize,
        int outputSize,
        int sequenceLength)
        where TFormat : IQuantizationFormat<TFormat>
        where TKernel : IQuantizedKernel<TFormat>
    {
        if (inputSize % TFormat.BlockSize != 0)
            throw new InvalidDataException($"Input size {inputSize} is not divisible by {TFormat.TensorType} block size {TFormat.BlockSize}.");

        int blocksPerColumn = inputSize / TFormat.BlockSize;
        int bytesPerColumn = checked(blocksPerColumn * TFormat.BytesPerBlock);
        Memory<byte> weightMemory = weights.Data;
        Memory<byte> activationMemory = activation.Data;
        Memory<byte> resultMemory = result.Data;

        BackendParallel.For(outputSize, 1, (start, end) =>
        {
            ReadOnlySpan<byte> weightBytes = weightMemory.Span;
            ReadOnlySpan<float> input = MemoryMarshal.Cast<byte, float>(activationMemory.Span);
            Span<float> output = MemoryMarshal.Cast<byte, float>(resultMemory.Span);

            for (int o = start; o < end; o++)
            {
                ReadOnlySpan<byte> column = weightBytes.Slice(checked(o * bytesPerColumn), bytesPerColumn);
                for (int seq = 0; seq < sequenceLength; seq++)
                {
                    ReadOnlySpan<float> row = input.Slice(checked(seq * inputSize), inputSize);
                    float sum = 0f;
                    for (int block = 0; block < blocksPerColumn; block++)
                    {
                        int bo = block * TFormat.BytesPerBlock;
                        int io = block * TFormat.BlockSize;
                        sum += TKernel.Dot(
                            column.Slice(bo, TFormat.BytesPerBlock),
                            row.Slice(io, TFormat.BlockSize));
                    }
                    output[seq * outputSize + o] = sum;
                }
            }
        });

        return true;
    }
}
