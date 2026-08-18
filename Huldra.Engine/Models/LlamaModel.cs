using Huldra.Engine.Backends;
using Huldra.Engine.Tensors;

namespace Huldra.Engine.Models;

public sealed class LlamaModel : IModel
{
    public required ModelConfig Config { get; init; }
    public required IReadOnlyDictionary<string, Tensor> Tensors { get; init; }
    public required ITokenizer Tokenizer { get; init; }

    public IContext CreateContext(int contextSize, IBackend backend)
    {
        // Initialize the inference context
        return new LlamaContext(this, backend, contextSize);
    }

    public string ApplyChatTemplate(string userPrompt)
    {
        // Qwen2.5 / Llama3 style ChatML
        return $"<|im_start|>user\n{userPrompt}<|im_end|>\n<|im_start|>assistant\n";
    }
}
