using Huldra.Engine.Quantization;

internal static class TensorFormatValidator
{
    internal static void Validate(
        TensorFormatDescriptor descriptor,
        Type implementationType)
    {
        if (descriptor.BlockSize <= 0)
        {
            throw new InvalidOperationException(
                $"Tensor format '{implementationType.FullName}' " +
                $"declares an invalid block size: " +
                $"{descriptor.BlockSize}.");
        }

        if (descriptor.BytesPerBlock <= 0)
        {
            throw new InvalidOperationException(
                $"Tensor format '{implementationType.FullName}' " +
                $"declares an invalid byte block size: " +
                $"{descriptor.BytesPerBlock}.");
        }

        if (descriptor.Dequantize is null)
        {
            throw new InvalidOperationException(
                $"Tensor format '{implementationType.FullName}' " +
                $"does not provide a dequantization delegate.");
        }

        if (!descriptor.IsQuantized &&
            descriptor.BlockSize != 1)
        {
            throw new InvalidOperationException(
                $"Non-quantized tensor format " +
                $"'{implementationType.FullName}' must have " +
                $"a block size of 1.");
        }
    }
}
