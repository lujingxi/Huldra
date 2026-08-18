using Huldra.Engine.Backends;
using Huldra.Engine.Tensors;

namespace Huldra.Engine.Models;

public interface IModel
{
    ModelConfig Config { get; }
    IReadOnlyDictionary<string, Tensor> Tensors { get; }
    ITokenizer Tokenizer { get; }

    IContext CreateContext(int contextSize, IBackend backend);
    string ApplyChatTemplate(string userPrompt);
}
