using Huldra.Engine.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Huldra.Engine.Tests;

public sealed class ModelArchitectureTests
{
    [Fact]
    public void Resolver_ShouldRecognizeGemma3()
    {
        ModelArchitecture result =
            ModelArchitectureResolver.Resolve("gemma3");

        Assert.Equal(ModelArchitecture.Gemma3, result);
    }

    [Fact]
    public void Resolver_ShouldRejectUnknownArchitecture()
    {
        Assert.Throws<NotSupportedException>(() =>
            ModelArchitectureResolver.Resolve("totally_unknown"));
    }
}
