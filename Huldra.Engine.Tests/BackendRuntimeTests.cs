using Huldra.Engine.Backends;
using Huldra.Engine.Scalar;
using Huldra.Engine.Tensors;
using Huldra.Engine.Vector;
using System;
using System.Collections.Generic;
using System.Text;

namespace Huldra.Engine.Tests;

public sealed class BackendRuntimeTests
{
    [Fact]
    public void GetBestBackend_ShouldReturnSupportedBackend()
    {
        IBackend backend = BackendRuntime.Instance.GetBestBackend();

        Assert.NotNull(backend);
    }

    [Fact]
    public void ScalarBackend_ShouldBeAvailable()
    {
        IBackend backend =
            BackendRuntime.Instance.GetBackend("Scalar");

        Assert.Equal("Scalar", backend.Name);
    }

    [Fact]
    public void VectorBackend_ShouldBeAvailable()
    {
        IBackend backend =
            BackendRuntime.Instance.GetBackend("Vector");

        Assert.Equal("Vector", backend.Name);
    }

    [Fact]
    public void BackendLookup_ShouldBeCaseInsensitive()
    {
        IBackend backend =
            BackendRuntime.Instance.GetBackend("vEcToR");

        Assert.Equal("Vector", backend.Name);
    }

    [Fact]
    public void UnknownBackend_ShouldThrow()
    {
        Assert.Throws<KeyNotFoundException>(
            () => BackendRuntime.Instance.GetBackend(
                "DefinitelyNotABackend"));
    }

    [Fact]
    public void SupportedBackends_ShouldContainBuiltInBackends()
    {
        IReadOnlyCollection<string> backends =
            BackendRuntime.Instance.SupportedBackends;

        Assert.Contains("Scalar", backends);
        Assert.Contains("Vector", backends);
    }

    [Fact]
    public void GetBackend_ShouldReturnSameInstance()
    {
        IBackend first =
            BackendRuntime.Instance.GetBackend("Vector");

        IBackend second =
            BackendRuntime.Instance.GetBackend("Vector");

        Assert.Same(first, second);
    }

    [Fact]
    public void GetBestBackend_ShouldReturnSameInstance()
    {
        IBackend first =
            BackendRuntime.Instance.GetBestBackend();

        IBackend second =
            BackendRuntime.Instance.GetBestBackend();

        Assert.Same(first, second);
    }

    [Fact]
    public void GetBestBackend_ShouldReturnHighestPrioritySupportedBackend()
    {
        BackendRuntime runtime = BackendRuntime.Instance;

        IBackend backend = runtime.GetBestBackend();

        Assert.NotNull(backend);
        Assert.True(backend.IsSupported);

        Assert.Equal(
            "Vector",
            backend.Name);
    }

    [Fact]
    public void GetBackend_ShouldReturnDiscoveredBackend()
    {
        BackendRuntime runtime = BackendRuntime.Instance;

        IBackend scalar = runtime.GetBackend("Scalar");
        IBackend vector = runtime.GetBackend("Vector");

        Assert.NotNull(scalar);
        Assert.NotNull(vector);

        Assert.Equal("Scalar", scalar.Name);
        Assert.Equal("Vector", vector.Name);

        Assert.True(scalar.IsSupported);
        Assert.True(vector.IsSupported);
    }
}
