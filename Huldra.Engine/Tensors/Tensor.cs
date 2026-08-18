using System.Runtime.InteropServices;

namespace Huldra.Engine.Tensors;

public sealed class Tensor
{
    public required TensorType Type { get; init; }
    public required int[] Shape { get; init; }
    public required Memory<byte> Data { get; init; }

    public int ElementCount
    {
        get
        {
            int count = 1;
            foreach (int dim in Shape)
            {
                if (dim < 0)
                    throw new InvalidOperationException("Tensor dimensions cannot be negative.");
                count = checked(count * dim);
            }
            return count;
        }
    }

    public bool IsF32 => Type == TensorType.F32;

    public Span<float> AsFloatSpan()
    {
        if (Type != TensorType.F32)
            throw new InvalidOperationException("Tensor is not of type F32.");

        int expectedBytes = checked(ElementCount * sizeof(float));
        if (Data.Length != expectedBytes)
            throw new InvalidDataException(
                $"F32 tensor has {Data.Length} data bytes, expected {expectedBytes} for shape [{string.Join(',', Shape)}].");

        return MemoryMarshal.Cast<byte, float>(Data.Span);
    }

    public void ValidateStorage()
    {
        if (Shape.Length == 0)
            throw new InvalidDataException("Tensor must have at least one dimension.");

        foreach (int dim in Shape)
        {
            if (dim <= 0)
                throw new InvalidDataException($"Tensor dimensions must be positive; got {dim}.");
        }

        long expectedBytes = TensorTypeInfo.GetStorageSize(Type, ElementCount);
        if (Data.Length != expectedBytes)
            throw new InvalidDataException(
                $"Tensor {Type} has {Data.Length} data bytes, expected {expectedBytes} for shape [{string.Join(',', Shape)}].");
    }
}
