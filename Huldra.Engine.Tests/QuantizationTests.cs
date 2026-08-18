using Huldra.Engine.Quantization;
using Huldra.Engine.Vector;
using Huldra.Engine.Vector.Quantization;
using Huldra.Engine.Tensors;
using System.Runtime.InteropServices;
using Xunit;

namespace Huldra.Engine.Tests;

public sealed class QuantizationTests
{
    [Fact]
    public void Q4_0_ScalarAndFormatDecode_ShouldMatch()
    {
        byte[] src = CreateQ4Block();
        float[] actual = new float[Q4_0.BlockSize];
        float[] expected = new float[Q4_0.BlockSize];

        Q4_0.DecodeBlock(src, actual);
        Q4_0.DecodeBlock(src, expected);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Q8_0_FormatDecode_ShouldMatchExpected()
    {
        byte[] src = CreateQ8Block();
        float[] actual = new float[Q8_0.BlockSize];
        Q8_0.DecodeBlock(src, actual);

        for (int i = 0; i < 32; i++)
            Assert.Equal(i - 16, actual[i]);
    }

    [Fact]
    public void Q4_0_DirectDot_ShouldMatchDequantizedReference()
    {
        byte[] src = CreateQ4Block();
        float[] activation = Enumerable.Range(0, 32).Select(i => (i - 15.5f) / 10f).ToArray();
        float[] decoded = new float[32];
        Q4_0.DecodeBlock(src, decoded);

        float expected = 0f;
        for (int i = 0; i < activation.Length; i++)
            expected += decoded[i] * activation[i];

        float actual = Q4_0VectorKernel.Dot(src, activation);
        Assert.InRange(MathF.Abs(expected - actual), 0f, 1e-5f);
    }

    [Fact]
    public void Q8_0_DirectDot_ShouldMatchDequantizedReference()
    {
        byte[] src = CreateQ8Block();
        float[] activation = Enumerable.Range(0, 32).Select(i => (i - 15.5f) / 10f).ToArray();
        float[] decoded = new float[32];
        Q8_0.DecodeBlock(src, decoded);

        float expected = 0f;
        for (int i = 0; i < activation.Length; i++)
            expected += decoded[i] * activation[i];

        float actual = Q8_0VectorKernel.Dot(src, activation);
        Assert.InRange(MathF.Abs(expected - actual), 0f, 1e-5f);
    }

    [Fact]
    public void SimdBackend_Q4_0MatMul_ShouldUseStaticKernelPath()
    {
        byte[] weights = new byte[18 * 2];
        for (int column = 0; column < 2; column++)
        {
            int offset = column * 18;
            weights[offset] = 0x00;
            weights[offset + 1] = 0x3C;
            for (int i = 0; i < 16; i++)
                weights[offset + 2 + i] = (byte)((i & 0x0F) | ((15 - i) << 4));
        }

        float[] activation = Enumerable.Range(0, 32).Select(i => (i - 15.5f) / 10f).ToArray();
        var weight = new Tensor { Type = TensorType.Q4_0, Shape = [32, 2], Data = weights };
        var input = new Tensor { Type = TensorType.F32, Shape = [1, 32], Data = MemoryMarshal.AsBytes(activation.AsSpan()).ToArray() };
        var output = new Tensor { Type = TensorType.F32, Shape = [1, 2], Data = new byte[2 * sizeof(float)] };

        var backend = new VectorBackend();
        backend.MatMul(weight, input, output);

        float expected = Q4_0VectorKernel.Dot(weights.AsSpan(0, 18), activation);
        float[] actual = output.AsFloatSpan().ToArray();

        Assert.Equal(expected, actual[0], 5);
        Assert.Equal(expected, actual[1], 5);
    }

    [Fact]
    public void BF16_ShouldDecodeCorrectly()
    {
        byte[] src = [0x00, 0x3F, 0x80, 0x3F, 0x00, 0x40, 0xC0, 0xBF];
        byte[] dst = new byte[4 * sizeof(float)];
        QuantizationRuntime.Dequantize<BF16>(src, dst);
        float[] actual = MemoryMarshal.Cast<byte, float>(dst).ToArray();
        Assert.Equal(0.5f, actual[0]);
        Assert.Equal(1.0f, actual[1]);
        Assert.Equal(2.0f, actual[2]);
        Assert.Equal(-1.5f, actual[3]);
    }

    [Fact]
    public void TensorValidation_ShouldRejectInvalidQuantizedShape()
    {
        var invalid = new Tensor { Type = TensorType.Q4_0, Shape = [31], Data = new byte[18] };
        Assert.Throws<InvalidDataException>(() => invalid.ValidateStorage());
    }

    private static byte[] CreateQ4Block()
    {
        byte[] src = new byte[Q4_0.BytesPerBlock];
        src[0] = 0x00; src[1] = 0x3C;
        for (int i = 0; i < 16; i++)
            src[2 + i] = (byte)((i & 0x0F) | ((15 - i) << 4));
        return src;
    }

    private static byte[] CreateQ8Block()
    {
        byte[] src = new byte[Q8_0.BytesPerBlock];
        src[0] = 0x00; src[1] = 0x3C;
        for (int i = 0; i < 32; i++)
            src[2 + i] = unchecked((byte)(sbyte)(i - 16));
        return src;
    }

    [Fact]
    public void QuantizationRuntime_DynamicType_ShouldDiscoverQ4_0()
    {
        byte[] source = CreateQ4Block();

        byte[] actualBytes =
            new byte[Q4_0.BlockSize * sizeof(float)];

        byte[] expectedBytes =
            new byte[Q4_0.BlockSize * sizeof(float)];

        QuantizationRuntime.Dequantize<Q4_0>(
            source,
            expectedBytes);

        QuantizationRuntime.Dequantize(
            TensorType.Q4_0,
            source,
            actualBytes);

        Assert.Equal(
            expectedBytes,
            actualBytes);
    }

    [Fact]
    public void TensorTypeInfo_ShouldComeFromDiscoveredFormat()
    {
        TensorTypeInfo info =
            TensorTypeInfo.For(TensorType.Q4_0);

        Assert.Equal(32, info.BlockSize);
        Assert.Equal(18, info.BytesPerBlock);
        Assert.True(info.IsQuantized);
    }

    [Fact]
    public void ValidFormat_ShouldBeAccepted()
    {
        TensorFormatRegistry registry =
            TensorFormatRegistry.Create(
            [
                typeof(Q4_0).Assembly
            ]);

        Assert.True(
            registry.TryGet(
                TensorType.Q4_0,
                out TensorFormatDescriptor descriptor));

        Assert.Equal(
            TensorType.Q4_0,
            descriptor.TensorType);

        Assert.Equal(
            Q4_0.BlockSize,
            descriptor.BlockSize);

        Assert.Equal(
            Q4_0.BytesPerBlock,
            descriptor.BytesPerBlock);

        Assert.True(descriptor.IsQuantized);

        Assert.NotNull(descriptor.Dequantize);
    }

    [Fact]
    public void TensorFormatValidator_ShouldAcceptValidDescriptor()
    {
        TensorFormatDescriptor descriptor =
            new(
                TensorType.Q4_0,
                BlockSize: 32,
                BytesPerBlock: 18,
                IsQuantized: true,
                Dequantize: static (_, _) => { });

        TensorFormatValidator.Validate(
            descriptor,
            typeof(Q4_0));
    }

    [Fact]
    public void TensorFormatValidator_ShouldRejectInvalidBlockSize()
    {
        TensorFormatDescriptor descriptor =
            new(
                TensorType.Q4_0,
                BlockSize: 0,
                BytesPerBlock: 18,
                IsQuantized: true,
                Dequantize: static (_, _) => { });

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    TensorFormatValidator.Validate(
                        descriptor,
                        typeof(Q4_0)));

        Assert.Contains(
            "invalid block size",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TensorFormatValidator_ShouldRejectInvalidBytesPerBlock()
    {
        TensorFormatDescriptor descriptor =
            new(
                TensorType.Q4_0,
                BlockSize: 32,
                BytesPerBlock: 0,
                IsQuantized: true,
                Dequantize: static (_, _) => { });

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    TensorFormatValidator.Validate(
                        descriptor,
                        typeof(Q4_0)));

        Assert.Contains(
            "invalid byte block size",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TensorFormatValidator_ShouldRejectInvalidNonQuantizedBlockSize()
    {
        TensorFormatDescriptor descriptor =
            new(
                TensorType.F16,
                BlockSize: 2,
                BytesPerBlock: 4,
                IsQuantized: false,
                Dequantize: static (_, _) => { });

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    TensorFormatValidator.Validate(
                        descriptor,
                        typeof(Q4_0)));

        Assert.Contains(
            "Non-quantized tensor format",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TensorFormatValidator_ShouldRejectMissingDequantizer()
    {
        TensorFormatDescriptor descriptor =
            new(
                TensorType.Q4_0,
                BlockSize: 32,
                BytesPerBlock: 18,
                IsQuantized: true,
                Dequantize: null!);

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    TensorFormatValidator.Validate(
                        descriptor,
                        typeof(Q4_0)));

        Assert.Contains(
            "dequantization delegate",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TensorFormatRegistry_ShouldDiscoverQ4_0()
    {
        TensorFormatRegistry registry =
            TensorFormatRegistry.Create(
            [
                typeof(Q4_0).Assembly
            ]);

        Assert.True(
            registry.TryGet(
                TensorType.Q4_0,
                out TensorFormatDescriptor descriptor));

        Assert.Equal(
            TensorType.Q4_0,
            descriptor.TensorType);

        Assert.Equal(
            Q4_0.BlockSize,
            descriptor.BlockSize);

        Assert.Equal(
            Q4_0.BytesPerBlock,
            descriptor.BytesPerBlock);

        Assert.True(descriptor.IsQuantized);
        Assert.NotNull(descriptor.Dequantize);
    }
}
