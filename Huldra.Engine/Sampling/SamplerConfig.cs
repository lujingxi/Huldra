namespace Huldra.Engine.Sampling;

public sealed class SamplerConfig
{
    public float Temperature { get; init; } = 0.7f;
    public int TopK { get; init; } = 40;
    public float TopP { get; init; } = 0.9f;
}
