using Huldra.Engine.Tensors;

namespace Huldra.Engine.Backends;

public static class BackendValidation
{
    public static void ValidateElementwise(Tensor a, Tensor b, Tensor result, string operation)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        ArgumentNullException.ThrowIfNull(result);

        a.ValidateStorage();
        b.ValidateStorage();
        result.ValidateStorage();

        if (!a.Shape.SequenceEqual(b.Shape) || !a.Shape.SequenceEqual(result.Shape))
            throw new ArgumentException($"{operation} requires tensors with identical shapes.");

        if (!a.IsF32 || !b.IsF32 || !result.IsF32)
            throw new NotSupportedException($"{operation} currently requires F32 tensors.");
    }

    public static (int Input, int Output, int SequenceLength) ValidateMatMul(Tensor weights, Tensor input, Tensor result)
    {
        ArgumentNullException.ThrowIfNull(weights);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(result);

        weights.ValidateStorage();
        input.ValidateStorage();
        result.ValidateStorage();

        if (weights.Shape.Length != 2 || input.Shape.Length != 2 || result.Shape.Length != 2)
            throw new ArgumentException("MatMul requires rank-2 tensors.");

        int inputSize = weights.Shape[0];
        int outputSize = weights.Shape[1];
        int sequenceLength = input.Shape[0];

        if (input.Shape[1] != inputSize)
            throw new ArgumentException(
                $"MatMul shape mismatch: weights are [{inputSize}, {outputSize}], input is [{input.Shape[0]}, {input.Shape[1]}].");

        if (!result.Shape.SequenceEqual(new[] { sequenceLength, outputSize }))
            throw new ArgumentException(
                $"MatMul result must have shape [{sequenceLength}, {outputSize}], got [{string.Join(',', result.Shape)}].");

        if (!result.IsF32)
            throw new NotSupportedException("MatMul currently requires an F32 result tensor.");

        return (inputSize, outputSize, sequenceLength);
    }
}

