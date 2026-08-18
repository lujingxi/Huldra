using Huldra.Engine.Backends;
using Huldra.Engine.Models;
using Huldra.Engine.Sampling;
using Huldra.Engine.Scalar;
using Huldra.Engine.Vector;
using System.Runtime.InteropServices;
using System.Text;

// Configure the console to handle UTF-8 data
Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

//Model Path
string[] paths =
[
    @"C:\Users\lujin\.lmstudio\models\Qwen\Qwen2.5-0.5B-Instruct-GGUF\qwen2.5-0.5b-instruct-fp16.gguf",
    @"C:\Users\lujin\.lmstudio\models\Qwen\Qwen2.5-0.5B-Instruct-GGUF\qwen2.5-0.5b-instruct-q4_0.gguf",
    @"C:\Users\lujin\.lmstudio\models\lmstudio-community\gemma-4-E2B-it-QAT-GGUF\gemma-4-E2B-it-QAT-Q4_0.gguf"
];
string modelPath = paths[1];

// 1. Initialize
IBackend backend = BackendRuntime.Instance.GetBackend("Scalar");

// 2. Load Model
Console.WriteLine("Loading model...");
IModel model = ModelFactory.Load(modelPath);
IContext context = model.CreateContext(512, backend); // Context size 512

// 3. Tokenize prompt
Console.Write("User: ");
string input = Console.ReadLine() ?? "Hello";
string prompt = model.ApplyChatTemplate(input);

int[] tokenArr = new int[4096];
int tokenCount = model.Tokenizer.Encode(prompt, tokenArr);
List<int> tokens = tokenArr[..tokenCount].ToList();

Console.Write("Tokens: ");
Console.WriteLine(string.Join(", ", tokens));

// 4.Initialize Sampler
var sampler = new Sampler(new SamplerConfig
{
    Temperature = 0.7f, // Try 0.1 to 1.0
    TopK = 40,
    TopP = 0.9f
});

Console.Write("AI: ");

// 5. Generation Loop
int maxTokens = 100;
for (int i = 0; i < maxTokens; i++)
{
    ReadOnlySpan<int> inputTokens = (i == 0)
        ? CollectionsMarshal.AsSpan(tokens)
        : CollectionsMarshal.AsSpan(tokens).Slice(tokens.Count - 1, 1);

    context.Evaluate(inputTokens);
    ReadOnlySpan<float> logits = context.GetLogits();

    // Use Sampler instead of Argmax
    int nextToken = sampler.Sample(logits);

    if (model.Tokenizer.EndOfSequenceTokenIds.Contains(nextToken))
        break;

    tokens.Add(nextToken);

    string decoded = model.Tokenizer.Decode(new[] { nextToken });
    Console.Write(decoded);
    Console.Out.Flush();
}

Console.WriteLine("\nGeneration finished.");
