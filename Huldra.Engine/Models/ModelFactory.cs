using Huldra.Engine.IO;
using Huldra.Engine.Tensors;

namespace Huldra.Engine.Models;

public static class ModelFactory
{
    public static IModel Load(string filePath)
    {
        var reader = new GgufReader();
        reader.Read(filePath);

        // Extract architecture name (e.g., "llama", "qwen2", "gemma")
        if (!reader.Metadata.TryGetValue("general.architecture", out var archObj) || archObj is not string arch)
        {
            throw new InvalidOperationException("Model architecture not found in metadata.");
        }

        ModelArchitecture architecture = ModelArchitectureResolver.Resolve(arch);
        var config = ExtractConfig(reader.Metadata);
        var tensors = LoadTensors(filePath, reader.Tensors);
        var tokenizer = new LlamaTokenizer(reader.Metadata);

        // Route to specific model implementation based on architecture
        return architecture switch
        {
            ModelArchitecture.Llama or
            ModelArchitecture.Qwen2 or
            ModelArchitecture.Qwen3 or
            ModelArchitecture.Mistral
                => new LlamaModel
                {
                    Config = config,
                    Tensors = tensors,
                    Tokenizer = tokenizer
                },

            ModelArchitecture.Gemma or
            ModelArchitecture.Gemma2 or
            ModelArchitecture.Gemma3 or
            ModelArchitecture.Gemma4
                => new GemmaModel
                {
                    Config = config,
                    Tensors = tensors,
                    Tokenizer = tokenizer
                },

            _ => throw new NotSupportedException(
                $"Architecture '{architecture}' is not supported.")
        };
    }

    private static ModelConfig ExtractConfig(Dictionary<string, object> metadata)
    {
        // FIX: Read block count first, so we can use it for array initializations
        int blockCount = FindRequiredInt(metadata, ".block_count");

        int embeddingLength = FindRequiredInt(metadata, ".embedding_length");
        int headCount = FindRequiredInt(metadata, ".attention.head_count");
        int headCountKv = FindInt(metadata, ".attention.head_count_kv");
        if (headCountKv == 0)
            headCountKv = headCount;
        int contextLength = FindRequiredInt(metadata, ".context_length");

        int[] feedForwardLength = FindIntArray(metadata, ".feed_forward_length", blockCount);
        if (feedForwardLength.Length != blockCount)
            throw new InvalidDataException(
                $"Expected {blockCount} feed-forward lengths, but GGUF provided {feedForwardLength.Length}.");

        return new ModelConfig
        {
            Architecture = metadata.TryGetValue("general.architecture", out var arch) ? arch as string ?? "" : "",
            EmbeddingLength = embeddingLength,
            BlockCount = blockCount,
            HeadCount = headCount,
            HeadCountKv = headCountKv,
            ContextLength = contextLength,
            RopeFreqBase = FindFloat(metadata, ".rope.freq_base", 10000f),
            RopeFreqScale = FindFloat(metadata, ".rope.freq_scale", 1.0f),
            VocabSize = metadata.TryGetValue("tokenizer.ggml.tokens", out var tokensVal) && tokensVal is object[] tokens
                ? tokens.Length
                : 0,
            FeedForwardLength = feedForwardLength
        };
    }

    private static int[] FindIntArray(Dictionary<string, object> metadata, string keySuffix, int blockCount)
    {
        var kvp = metadata.FirstOrDefault(k => k.Key.EndsWith(keySuffix, StringComparison.OrdinalIgnoreCase));
        if (kvp.Value == null) return new int[blockCount]; // Fallback empty

        if (kvp.Value is object[] arr)
        {
            return arr.Select(v => Convert.ToInt32(v)).ToArray();
        }
        else
        {
            // If it's a single value (like Llama/Qwen), create an array filled with this value
            int val = Convert.ToInt32(kvp.Value);
            return Enumerable.Repeat(val, blockCount).ToArray();
        }
    }

    private static Dictionary<string, Tensor> LoadTensors(string filePath, List<GgufTensorInfo> tensorInfos)
    {
        var tensors = new Dictionary<string, Tensor>();
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

        foreach (var info in tensorInfos)
        {
            if (info.SizeInBytes > int.MaxValue)
                throw new NotSupportedException(
                    $"Tensor '{info.Name}' is {info.SizeInBytes:N0} bytes; the current in-memory Tensor storage is limited to {int.MaxValue:N0} bytes.");

            byte[] buffer = new byte[(int)info.SizeInBytes];
            stream.Seek(info.DataOffset, SeekOrigin.Begin);
            stream.ReadExactly(buffer);

            var tensor = new Tensor
            {
                Type = info.Type,
                Shape = info.Shape,
                Data = buffer
            };
            tensor.ValidateStorage();
            tensors.Add(info.Name, tensor);
        }
        return tensors;
    }

    private static int FindInt(Dictionary<string, object> metadata, string keySuffix)
    {
        var kvp = metadata.FirstOrDefault(k => k.Key.EndsWith(keySuffix, StringComparison.OrdinalIgnoreCase));
        return kvp.Value is null ? 0 : Convert.ToInt32(kvp.Value);
    }

    private static int FindRequiredInt(Dictionary<string, object> metadata, string keySuffix)
    {
        int value = FindInt(metadata, keySuffix);
        if (value <= 0)
            throw new InvalidDataException($"Required GGUF metadata '{keySuffix}' was not found or is invalid.");
        return value;
    }

    private static float FindFloat(Dictionary<string, object> metadata, string keySuffix, float defaultValue)
    {
        var kvp = metadata.FirstOrDefault(k => k.Key.EndsWith(keySuffix, StringComparison.OrdinalIgnoreCase));
        return kvp.Value != null ? Convert.ToSingle(kvp.Value) : defaultValue;
    }
}
