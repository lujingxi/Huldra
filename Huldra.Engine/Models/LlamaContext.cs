using System.Runtime.InteropServices;
using Huldra.Engine.Backends;
using Huldra.Engine.Quantization;
using Huldra.Engine.Tensors;

namespace Huldra.Engine.Models;

public sealed class LlamaContext : IContext
{
    private readonly LlamaModel _model;
    private readonly IBackend _backend;

    private readonly Tensor[] _kvCacheK;
    private readonly Tensor[] _kvCacheV;

    private readonly Tensor _hiddenStates;
    private Tensor? _logits;

    private int _currentSeqLen = 0;

    public LlamaContext(LlamaModel model, IBackend backend, int contextSize)
    {
        _model = model;
        _backend = backend;

        int numLayers = model.Config.BlockCount;
        int headCountKv = model.Config.HeadCountKv;
        int headDim = model.Config.HeadDimension;

        _kvCacheK = new Tensor[numLayers];
        _kvCacheV = new Tensor[numLayers];

        for (int i = 0; i < numLayers; i++)
        {
            // Fix: Shape should match our Row-major indexing in Attention method
            // [ContextSize, HeadCountKv, HeadDim]
            int[] kvShape = [contextSize, headCountKv, headDim];
            long kvSize = (long)headDim * headCountKv * contextSize * 4L;

            _kvCacheK[i] = new Tensor { Type = TensorType.F32, Shape = kvShape, Data = new byte[kvSize] };
            _kvCacheV[i] = new Tensor { Type = TensorType.F32, Shape = kvShape, Data = new byte[kvSize] };
        }

        int embdLength = model.Config.EmbeddingLength;
        _hiddenStates = new Tensor
        {
            Type = TensorType.F32,
            Shape = [embdLength, contextSize],
            Data = new byte[embdLength * contextSize * 4L]
        };
    }

    public void Evaluate(ReadOnlySpan<int> tokens)
    {
        int seqLen = tokens.Length;
        int startPos = _currentSeqLen; // New: Start position for this batch

        int embdLength = _model.Config.EmbeddingLength;
        int headCount = _model.Config.HeadCount;
        int headCountKv = _model.Config.HeadCountKv;
        int headDim = _model.Config.HeadDimension;
        int qDim = headCount * headDim;
        int kvDim = headCountKv * headDim;
        int numLayers = _model.Config.BlockCount;

        // --- 1. Token Embedding Lookup ---
        if (!_model.Tensors.TryGetValue("token_embd.weight", out var embdWeight))
            throw new InvalidOperationException("token_embd.weight not found.");

        Tensor currentHidden = new Tensor
        {
            Type = TensorType.F32,
            Shape = [seqLen, embdLength],
            Data = new byte[seqLen * embdLength * 4]
        };

        // FIX: Dynamically get the correct quantizer based on the tensor's actual type

        // FIX: Calculate the byte size of a single token's embedding vector based on its type
        long tokenEmbdSizeBytes = embdWeight.Type switch
        {
            TensorType.F32 => embdLength * 4L,
            TensorType.F16 => embdLength * 2L,
            TensorType.Q4_0 => (embdLength / 32) * 18L,
            TensorType.Q8_0 => (embdLength / 32) * 34L,
            TensorType.Q6_K => (embdLength / 256) * 210L,
            _ => throw new NotSupportedException($"Token embedding type {embdWeight.Type} is not supported for lookup.")
        };

        for (int i = 0; i < seqLen; i++)
        {
            int tokenId = tokens[i];
            long byteOffset = (long)tokenId * tokenEmbdSizeBytes;

            // Slice the exact byte size for this token
            ReadOnlyMemory<byte> tokenEmbdMemory = embdWeight.Data.Slice((int)byteOffset, (int)tokenEmbdSizeBytes);
            Memory<byte> dstMemory = currentHidden.Data.Slice(i * embdLength * 4, embdLength * 4);

            // Use the dynamically resolved quantizer
            QuantizationRuntime.Dequantize(embdWeight.Type, tokenEmbdMemory, dstMemory);
        }

        // --- 2. Transformer Blocks ---
        for (int layer = 0; layer < numLayers; layer++)
        {
            string prefix = $"blk.{layer}.";

            if (!_model.Tensors.TryGetValue($"{prefix}attn_norm.weight", out var attnNormWeight))
                throw new InvalidOperationException($"{prefix}attn_norm.weight not found.");

            Tensor normOutput = new Tensor { Type = TensorType.F32, Shape = [seqLen, embdLength], Data = new byte[seqLen * embdLength * 4] };
            _backend.RMSNorm(currentHidden, attnNormWeight, normOutput, 1e-6f);

            if (!_model.Tensors.TryGetValue($"{prefix}attn_q.weight", out var wq) ||
                !_model.Tensors.TryGetValue($"{prefix}attn_k.weight", out var wk) ||
                !_model.Tensors.TryGetValue($"{prefix}attn_v.weight", out var wv))
                throw new InvalidOperationException($"{prefix} attention weights not found.");

            Tensor q = new Tensor { Type = TensorType.F32, Shape = [seqLen, qDim], Data = new byte[seqLen * qDim * 4] };
            Tensor k = new Tensor { Type = TensorType.F32, Shape = [seqLen, kvDim], Data = new byte[seqLen * kvDim * 4] };
            Tensor v = new Tensor { Type = TensorType.F32, Shape = [seqLen, kvDim], Data = new byte[seqLen * kvDim * 4] };

            _backend.MatMul(wq, normOutput, q);
            _backend.MatMul(wk, normOutput, k);
            _backend.MatMul(wv, normOutput, v);

            if (_model.Tensors.TryGetValue($"{prefix}attn_q.bias", out var bq)) _backend.AddBias(bq, q);
            if (_model.Tensors.TryGetValue($"{prefix}attn_k.bias", out var bk)) _backend.AddBias(bk, k);
            if (_model.Tensors.TryGetValue($"{prefix}attn_v.bias", out var bv)) _backend.AddBias(bv, v);

            // Pass startPos to RoPE and Attention
            _backend.RoPE(q, k, headCount, headCountKv, headDim, _model.Config.RopeFreqBase, startPos);

            Tensor attnOutput = new Tensor { Type = TensorType.F32, Shape = [seqLen, qDim], Data = new byte[seqLen * qDim * 4] };
            _backend.Attention(q, k, v, _kvCacheK[layer], _kvCacheV[layer], attnOutput, headCount, headCountKv, headDim, seqLen, startPos);

            if (!_model.Tensors.TryGetValue($"{prefix}attn_output.weight", out var wo))
                throw new InvalidOperationException($"{prefix}attn_output.weight not found.");

            Tensor projOutput = new Tensor { Type = TensorType.F32, Shape = [seqLen, embdLength], Data = new byte[seqLen * embdLength * 4] };
            _backend.MatMul(wo, attnOutput, projOutput);

            _backend.Add(currentHidden, projOutput, currentHidden);

            if (!_model.Tensors.TryGetValue($"{prefix}ffn_norm.weight", out var ffnNormWeight))
                throw new InvalidOperationException($"{prefix}ffn_norm.weight not found.");

            Tensor mlpNormOutput = new Tensor { Type = TensorType.F32, Shape = [seqLen, embdLength], Data = new byte[seqLen * embdLength * 4] };
            _backend.RMSNorm(currentHidden, ffnNormWeight, mlpNormOutput, 1e-6f);

            int ffDim = _model.Config.FeedForwardLength[layer];

            if (!_model.Tensors.TryGetValue($"{prefix}ffn_gate.weight", out var wGate) ||
                !_model.Tensors.TryGetValue($"{prefix}ffn_up.weight", out var wUp) ||
                !_model.Tensors.TryGetValue($"{prefix}ffn_down.weight", out var wDown))
                throw new InvalidOperationException($"{prefix} MLP weights not found.");

            Tensor gate = new Tensor { Type = TensorType.F32, Shape = [seqLen, ffDim], Data = new byte[seqLen * ffDim * 4] };
            Tensor up = new Tensor { Type = TensorType.F32, Shape = [seqLen, ffDim], Data = new byte[seqLen * ffDim * 4] };

            _backend.MatMul(wGate, mlpNormOutput, gate);
            _backend.MatMul(wUp, mlpNormOutput, up);
            _backend.SiLU(gate);

            Tensor mulOut = new Tensor { Type = TensorType.F32, Shape = [seqLen, ffDim], Data = new byte[seqLen * ffDim * 4] };
            _backend.Mul(gate, up, mulOut);

            Tensor mlpOutput = new Tensor { Type = TensorType.F32, Shape = [seqLen, embdLength], Data = new byte[seqLen * embdLength * 4] };
            _backend.MatMul(wDown, mulOut, mlpOutput);

            _backend.Add(currentHidden, mlpOutput, currentHidden);
        }

        // --- 3. Final RMSNorm ---
        if (!_model.Tensors.TryGetValue("output_norm.weight", out var outNormWeight))
            throw new InvalidOperationException("output_norm.weight not found.");

        Tensor finalNorm = new Tensor { Type = TensorType.F32, Shape = [seqLen, embdLength], Data = new byte[seqLen * embdLength * 4] };
        _backend.RMSNorm(currentHidden, outNormWeight, finalNorm, 1e-6f);

        // --- 4. Output Projection (Logits) ---
        if (!_model.Tensors.TryGetValue("output.weight", out var outWeight))
            throw new InvalidOperationException("output.weight not found.");

        int vocabSize = _model.Config.VocabSize;
        _logits = new Tensor { Type = TensorType.F32, Shape = [seqLen, vocabSize], Data = new byte[seqLen * vocabSize * 4] };

        _backend.MatMul(outWeight, finalNorm, _logits);

        // FIX: Update current sequence length after evaluation
        _currentSeqLen += seqLen;
    }

    public ReadOnlySpan<float> GetLogits()
    {
        if (_logits is null) throw new InvalidOperationException("Evaluate must be called before getting logits.");

        // Return only the logits for the LAST token in the sequence
        int vocabSize = _model.Config.VocabSize;
        // In Row-major [SeqLen, VocabSize], the last token is at index (seqLen - 1) * vocabSize
        int lastTokenOffset = (_logits.Shape[0] - 1) * vocabSize;

        return _logits.AsFloatSpan().Slice(lastTokenOffset, vocabSize);
    }
}