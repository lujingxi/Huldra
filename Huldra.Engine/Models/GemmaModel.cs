using Huldra.Engine.Backends;
using Huldra.Engine.Tensors;

namespace Huldra.Engine.Models;

public sealed class GemmaModel : IModel
{
    public required ModelConfig Config { get; init; }
    public required IReadOnlyDictionary<string, Tensor> Tensors { get; init; }
    public required ITokenizer Tokenizer { get; init; }

    public IContext CreateContext(int contextSize, IBackend backend)
    {
        return new GemmaContext(this, backend, contextSize);
    }

    public string ApplyChatTemplate(string userPrompt)
    {
        // Gemma 2 style chat template
        return $"<start_of_turn>user\n{userPrompt}<end_of_turn>\n<start_of_turn>model\n";
    }
}
