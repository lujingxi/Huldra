using Huldra.Engine.Backends;
using Huldra.Engine.Scalar;
using Huldra.Engine.Tensors;
using Huldra.Engine.Vector;
using System;
using System.Collections.Generic;
using System.Text;

namespace Huldra.Engine.Tests;

public sealed class BackendDiscoveryTests
{
    [Fact]
    public void Discover_ShouldDiscoverScalarBackend()
    {
        IReadOnlyCollection<BackendDescriptor> backends =
            BackendDiscovery.Discover(
                [typeof(ScalarBackend).Assembly]);

        BackendDescriptor descriptor =
            Assert.Single(backends, static x => x.BackendType == typeof(ScalarBackend));

        Assert.Equal("Scalar", descriptor.Name);
        Assert.Equal(0, descriptor.Priority);
        Assert.NotNull(descriptor.Instance);
        Assert.IsType<ScalarBackend>(descriptor.Instance);
    }

    [Fact]
    public void Discover_ShouldDiscoverVectorBackend()
    {
        IReadOnlyCollection<BackendDescriptor> backends =
            BackendDiscovery.Discover(
                [typeof(VectorBackend).Assembly]);

        BackendDescriptor descriptor =
            Assert.Single(backends, static x => x.BackendType == typeof(VectorBackend));

        Assert.Equal("Vector", descriptor.Name);
        Assert.Equal(100, descriptor.Priority);
        Assert.NotNull(descriptor.Instance);
        Assert.IsType<VectorBackend>(descriptor.Instance);
    }

    [Fact]
    public void Discover_ShouldIgnoreAbstractBackend()
    {
        IReadOnlyCollection<BackendDescriptor> backends =
            BackendDiscovery.Discover(
                [typeof(BackendDiscoveryTests).Assembly]);

        Assert.DoesNotContain(
            backends,
            static descriptor =>
                descriptor.BackendType ==
                typeof(AbstractBackend));
    }

    [Fact]
    public void Discover_ShouldIgnoreBackendWithoutPublicParameterlessConstructor()
    {
        IReadOnlyCollection<BackendDescriptor> backends =
            BackendDiscovery.Discover(
                [typeof(BackendDiscoveryTests).Assembly]);

        Assert.DoesNotContain(
            backends,
            static descriptor =>
                descriptor.BackendType ==
                typeof(InvalidBackend));
    }

    private abstract class AbstractBackend : IBackend
    {
        public int Priority => 999;
        public abstract string Name { get; }
        public bool IsSupported => true;

        public abstract void MatMul(
            Tensor a, Tensor b, Tensor result);

        public abstract void RMSNorm(
            Tensor input,
            Tensor weight,
            Tensor output,
            float epsilon);

        public abstract void RoPE(
            Tensor q,
            Tensor k,
            int headCount,
            int headCountKv,
            int headDim,
            float ropeFreqBase,
            int startPos);

        public abstract void Attention(
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
            int startPos);

        public abstract void SiLU(Tensor tensor);
        public abstract void Gelu(Tensor tensor);
        public abstract void Mul(Tensor a, Tensor b, Tensor result);
        public abstract void Add(Tensor a, Tensor b, Tensor result);
        public abstract void AddBias(Tensor bias, Tensor tensor);
    }

    private sealed class InvalidBackend : IBackend
    {
        private InvalidBackend(string value)
        {
        }

        public int Priority => 998;

        public string Name => "Invalid";
        public bool IsSupported => true;

        public void MatMul(Tensor a, Tensor b, Tensor result)
            => throw new NotImplementedException();

        public void RMSNorm(
            Tensor input,
            Tensor weight,
            Tensor output,
            float epsilon)
            => throw new NotImplementedException();

        public void RoPE(
            Tensor q,
            Tensor k,
            int headCount,
            int headCountKv,
            int headDim,
            float ropeFreqBase,
            int startPos)
            => throw new NotImplementedException();

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
            => throw new NotImplementedException();

        public void SiLU(Tensor tensor)
            => throw new NotImplementedException();

        public void Gelu(Tensor tensor)
            => throw new NotImplementedException();

        public void Mul(Tensor a, Tensor b, Tensor result)
            => throw new NotImplementedException();

        public void Add(Tensor a, Tensor b, Tensor result)
            => throw new NotImplementedException();

        public void AddBias(Tensor bias, Tensor tensor)
            => throw new NotImplementedException();
    }
}
