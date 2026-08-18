namespace Huldra.Engine.Models;

public sealed class ModelConfig
{
    public string Architecture { get; init; } = "";
    public int EmbeddingLength { get; init; }
    public int BlockCount { get; init; }
    public int HeadCount { get; init; }
    public int HeadCountKv { get; init; }
    public int ContextLength { get; init; }
    public float RopeFreqBase { get; init; }
    public float RopeFreqScale { get; init; }
    public int VocabSize { get; init; }
    public int[] FeedForwardLength { get; init; } = [];

    public int HeadDimension
    {
        get
        {
            if (HeadCount <= 0 || EmbeddingLength <= 0 || EmbeddingLength % HeadCount != 0)
                throw new InvalidOperationException(
                    $"Invalid attention configuration: embedding_length={EmbeddingLength}, head_count={HeadCount}.");
            return EmbeddingLength / HeadCount;
        }
    }
}
