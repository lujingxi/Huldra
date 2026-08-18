namespace Huldra.Engine.Tensors;

public static class TensorValidation
{
    public static void RequireF32(Tensor tensor, string parameterName)
    {
        if (tensor.Type != TensorType.F32)
            throw new ArgumentException($"{parameterName} must be an F32 tensor, but is {tensor.Type}.", parameterName);

        _ = tensor.AsFloatSpan();
    }

    public static void RequireSameShape(Tensor a, Tensor b, string operation)
    {
        if (!a.Shape.AsSpan().SequenceEqual(b.Shape))
            throw new ArgumentException($"{operation} requires matching tensor shapes; got [{string.Join(',', a.Shape)}] and [{string.Join(',', b.Shape)}].");
    }

    public static void RequireSameShape(Tensor a, Tensor b, Tensor result, string operation)
    {
        RequireSameShape(a, b, operation);
        RequireSameShape(a, result, operation);
    }
}
