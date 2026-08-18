using System;
using System.Collections.Generic;
using System.Text;

namespace Huldra.Engine.Models;

public static class ModelArchitectureResolver
{
    public static ModelArchitecture Resolve(string architecture)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(architecture);

        return architecture switch
        {
            "llama" => ModelArchitecture.Llama,
            "qwen2" => ModelArchitecture.Qwen2,
            "qwen3" => ModelArchitecture.Qwen3,
            "mistral" => ModelArchitecture.Mistral,
            "gemma" => ModelArchitecture.Gemma,
            "gemma2" => ModelArchitecture.Gemma2,
            "gemma3" => ModelArchitecture.Gemma3,
            "gemma4" => ModelArchitecture.Gemma4,

            _ => throw new NotSupportedException(
                $"Architecture '{architecture}' is not supported.")
        };
    }
}
