namespace Huldra.Cli;

internal class EngineTest
{
    //private const string ModelPath = @"C:\Users\lujin\.lmstudio\models\unsloth\Qwen3.6-27B-GGUF\Qwen3.6-27B-UD-Q4_K_XL.gguf";
    //private const string ModelPath = @"C:\Users\lujin\.lmstudio\models\lmstudio-community\Qwen3.6-27B-GGUF\Qwen3.6-27B-Q4_K_M.gguf";
    private const string ModelPath = @"C:\Users\lujin\.lmstudio\models\lmstudio-community\Qwen2.5-7B-Instruct-GGUF\Qwen2.5-7B-Instruct-Q4_K_M.gguf";

    //public static unsafe void DiagnoseDeepToken()
    //{
    //    Console.WriteLine("======================================");
    //    Console.WriteLine(" Huldra Engine - Deep Token Diagnostic");
    //    Console.WriteLine("======================================");

    //    using IBackend backend = new ScalarBackend();
    //    string modelPath = ModelPath;

    //    try
    //    {
    //        using var reader = new GgufReader(modelPath);
    //        reader.Parse();

    //        var tokenizer = new LlmTokenizer(reader);
    //        var qwen = new QwenModel(reader);
    //        var scalarBackend = (ScalarBackend)backend;

    //        using var kvCache = new LlmKvCache(qwen.Blocks.Length, maxSeqLen: 512, kvDim: (int)qwen.HiddenSize);

    //        Console.ForegroundColor = ConsoleColor.Yellow;
    //        Console.Write("\nEnter your Text Prompt: ");
    //        string rawPrompt = Console.ReadLine() ?? "Hello, how are you?";
    //        Console.ResetColor();

    //        string formattedPrompt = tokenizer.ApplyChatTemplate(rawPrompt, systemPrompt: "You are AI.");
    //        List<int> promptTokens = tokenizer.Tokenize(formattedPrompt);

    //        Console.WriteLine($"\n[Chat Template Formatted Prompt]:\n{formattedPrompt}");
    //        Console.WriteLine($"[Tokenizer BPE] Encoded into {promptTokens.Count} Token IDs:");

    //        for (int i = 0; i < promptTokens.Count; i++)
    //        {
    //            Console.WriteLine($"   Token {i,2}: ID {promptTokens[i],6} -> \"{tokenizer.Detokenize(promptTokens[i])}\"");
    //        }

    //        float ropeBase = reader.GetMetadata<float>("qwen2.rope.freq_base");
    //        if (ropeBase == 0) ropeBase = 1000000.0f;

    //        var sw = Stopwatch.StartNew();

    //        using ITensor hiddenState = backend.AllocateTensor("hidden_state", DataType.F32, (int)qwen.HiddenSize);
    //        Span<float> hiddenSpan = new Span<float>((void*)hiddenState.DataPointer, (int)qwen.HiddenSize);

    //        // ========================================================
    //        // STAGE 1: PROMPT PRE-FILL PHASE
    //        // ========================================================
    //        Console.WriteLine("\n[Engine] Pre-filling Prompt Tokens into KV Cache...");
    //        for (int pos = 0; pos < promptTokens.Count; pos++)
    //        {
    //            int currentTokenId = promptTokens[pos];
    //            RunForwardPassForToken(currentTokenId, pos, qwen, scalarBackend, backend, kvCache, hiddenState, hiddenSpan, ropeBase);
    //        }

    //        // ========================================================
    //        // STAGE 2: AUTO-REGRESSIVE GENERATION STREAM
    //        // ========================================================
    //        Console.ForegroundColor = ConsoleColor.Green;
    //        Console.WriteLine($"\n[Huldra Assistant Stream]:");

    //        int startPos = promptTokens.Count;
    //        int tokensToGenerate = 5;
    //        int currentPos = startPos;

    //        for (int step = 0; step < tokensToGenerate; step++)
    //        {
    //            // A. Final Output Norm
    //            ITensor finalNormWeight = qwen.OutputNorm ?? throw new Exception("OutputNorm missing.");
    //            using ITensor finalNormOutput = backend.AllocateTensor("final_norm", DataType.F32, (int)qwen.HiddenSize);
    //            backend.RMSNorm(hiddenState, finalNormWeight, finalNormOutput, 1e-6f);

    //            Span<float> fnSpan = new Span<float>((void*)finalNormOutput.DataPointer, (int)qwen.HiddenSize);
    //            Console.WriteLine($"\n[LM Head Input Check] finalNormOutput [0..4]: [{fnSpan[0]:E4}, {fnSpan[1]:E4}, {fnSpan[2]:E4}, {fnSpan[3]:E4}, {fnSpan[4]:E4}]");

    //            // B. LM Head Projection
    //            ITensor lmHeadWeight = qwen.Output ?? qwen.TokenEmbeddings!;
    //            int vocabSize = tokenizer.GetVocabSize();
    //            using ITensor logitsTensor = backend.AllocateTensor("logits", DataType.F32, vocabSize);

    //            Console.WriteLine($"[LM Head Debug] Tensor: {lmHeadWeight.Name} | Type: {lmHeadWeight.DataType} | MaxByteSize: {lmHeadWeight.MaxByteSize} bytes");
    //            Console.WriteLine($"[LM Head] Projecting Final Vector into {vocabSize} Vocabulary Logits on CPU...");

    //            scalarBackend.QuantizedMatVecMul(finalNormOutput, lmHeadWeight, logitsTensor);

    //            // C. Diagnostic Print Top 3 Logits for this step
    //            Span<float> logitsSpan = new Span<float>((void*)logitsTensor.DataPointer, vocabSize);
    //            var top3 = logitsSpan.ToArray()
    //                .Select((val, idx) => (Val: val, Idx: idx))
    //                .OrderByDescending(x => x.Val)
    //                .Take(3)
    //                .ToList();

    //            Console.Write($"[Step {step + 1}] Top Candidates: ");
    //            foreach (var c in top3)
    //            {
    //                Console.Write($"\"{tokenizer.Detokenize(c.Idx)}\"({c.Val:F1}) ");
    //            }

    //            // D. Sample Next Token
    //            int nextTokenId = LlmSampler.Sample(logitsSpan, temperature: 0.7f, topP: 0.9f);
    //            string wordText = tokenizer.Detokenize(nextTokenId);

    //            Console.ForegroundColor = ConsoleColor.Yellow;
    //            Console.WriteLine($" -> Selected: \"{wordText}\"");
    //            Console.ForegroundColor = ConsoleColor.Green;

    //            if (nextTokenId == tokenizer.EosTokenId) break;

    //            // E. Run Forward Pass for newly predicted token
    //            RunForwardPassForToken(nextTokenId, currentPos, qwen, scalarBackend, backend, kvCache, hiddenState, hiddenSpan, ropeBase);
    //            currentPos++;
    //        }

    //        Console.ResetColor();
    //        sw.Stop();
    //        Console.WriteLine($"\n\n[Generation Complete] Total Time: {sw.Elapsed.TotalSeconds:F2}s");

    //    }
    //    catch (Exception ex)
    //    {
    //        Console.ForegroundColor = ConsoleColor.Red;
    //        Console.WriteLine($"\n[Error] {ex.Message}");
    //        Console.ResetColor();
    //    }

    //    Console.WriteLine("\nPress any key to exit...");
    //    Console.ReadKey();

    //    // Helper method
    //    static void RunForwardPassForToken(
    //        int tokenId,
    //        int pos,
    //        QwenModel qwen,
    //        ScalarBackend scalarBackend,
    //        IBackend backend,
    //        LlmKvCache kvCache,
    //        ITensor hiddenState,
    //        Span<float> hiddenSpan,
    //        float ropeBase)
    //    {
    //        ITensor embdWeight = qwen.TokenEmbeddings ?? throw new Exception("Embeddings missing.");
    //        long bytesPerRow = TensorHelper.CalculateRowStrideBytes(embdWeight.DataType, (int)qwen.HiddenSize);
    //        IntPtr rowPointer = (IntPtr)((byte*)embdWeight.DataPointer + (tokenId * bytesPerRow));

    //        using var rowTensor = new Tensor("temp_row", embdWeight.DataType, [(int)qwen.HiddenSize], rowPointer, bytesPerRow);
    //        scalarBackend.DequantizeTensor(rowTensor, hiddenSpan);

    //        if (pos == 0 || pos == 21)
    //        {
    //            Console.WriteLine($"\n[Token {pos} Diagnostic] Raw Embedding [0..2]: [{hiddenSpan[0]:E4}, {hiddenSpan[1]:E4}, {hiddenSpan[2]:E4}]");
    //        }

    //        for (int layerIdx = 0; layerIdx < qwen.Blocks.Length; layerIdx++)
    //        {
    //            var block = qwen.Blocks[layerIdx];

    //            using ITensor norm1Output = backend.AllocateTensor("norm1", DataType.F32, (int)qwen.HiddenSize);
    //            backend.RMSNorm(hiddenState, block.AttnNorm!, norm1Output, 1e-6f);

    //            int hiddenDim = (int)qwen.HiddenSize;
    //            using ITensor queryVector = backend.AllocateTensor("query", DataType.F32, hiddenDim);

    //            int kDim = block.AttnK?.Shape[1] ?? (hiddenDim / 2);
    //            int vDim = block.AttnV?.Shape[1] ?? (hiddenDim / 2);

    //            using ITensor keyVector = backend.AllocateTensor("key", DataType.F32, kDim);
    //            using ITensor valueVector = backend.AllocateTensor("value", DataType.F32, vDim);

    //            unsafe
    //            {
    //                Span<float> qSpan = new Span<float>((void*)queryVector.DataPointer, hiddenDim);
    //                Span<float> kSpan = new Span<float>((void*)keyVector.DataPointer, kDim);
    //                Span<float> vSpan = new Span<float>((void*)valueVector.DataPointer, vDim);

    //                if (block.AttnQkv != null)
    //                {
    //                    int qkvDim = block.AttnQkv.Shape[1];
    //                    using ITensor qkvVector = backend.AllocateTensor("qkv", DataType.F32, qkvDim);
    //                    scalarBackend.QuantizedMatVecMul(norm1Output, block.AttnQkv, qkvVector);

    //                    Span<float> qkvSpan = new Span<float>((void*)qkvVector.DataPointer, qkvDim);

    //                    int kSize = (qkvDim - hiddenDim) / 2;
    //                    int vSize = kSize;

    //                    qkvSpan.Slice(0, hiddenDim).CopyTo(qSpan);
    //                    qkvSpan.Slice(hiddenDim, kSize).CopyTo(kSpan);
    //                    qkvSpan.Slice(hiddenDim + kSize, vSize).CopyTo(vSpan);
    //                }
    //                else
    //                {
    //                    scalarBackend.QuantizedMatVecMul(norm1Output, block.AttnQ!, queryVector);
    //                    scalarBackend.QuantizedMatVecMul(norm1Output, block.AttnK!, keyVector);
    //                    scalarBackend.QuantizedMatVecMul(norm1Output, block.AttnV!, valueVector);
    //                }

    //                if (block.AttnQNorm != null)
    //                {
    //                    using ITensor normQuery = backend.AllocateTensor("norm_q", DataType.F32, hiddenDim);
    //                    backend.RMSNorm(queryVector, block.AttnQNorm, normQuery, 1e-6f);
    //                    Span<float> nqSpan = new Span<float>((void*)normQuery.DataPointer, hiddenDim);
    //                    nqSpan.CopyTo(qSpan);
    //                }

    //                if (block.AttnKNorm != null)
    //                {
    //                    using ITensor normKey = backend.AllocateTensor("norm_k", DataType.F32, kDim);
    //                    backend.RMSNorm(keyVector, block.AttnKNorm, normKey, 1e-6f);
    //                    Span<float> nkSpan = new Span<float>((void*)normKey.DataPointer, kDim);
    //                    nkSpan.CopyTo(kSpan);
    //                }

    //                int queryHeadCount = (int)qwen.HiddenSize / 128;
    //                scalarBackend.RoPE(queryVector, position: pos, headCount: queryHeadCount, ropeBase: ropeBase);
    //                scalarBackend.RoPE(keyVector, position: pos, headCount: queryHeadCount, ropeBase: ropeBase);

    //                ReadOnlySpan<float> kStoreSpan = new ReadOnlySpan<float>((void*)keyVector.DataPointer, kDim);
    //                ReadOnlySpan<float> vStoreSpan = new ReadOnlySpan<float>((void*)valueVector.DataPointer, vDim);
    //                kvCache.Store(layerIdx, pos, kStoreSpan, vStoreSpan);

    //                using ITensor attnContextVector = backend.AllocateTensor("attn_ctx", DataType.F32, hiddenDim);
    //                scalarBackend.ComputeAttentionWithKvCache(queryVector, kvCache, layerIdx, pos, queryHeadCount, attnContextVector);

    //                if (block.AttnGate != null)
    //                {
    //                    int gateDim = block.AttnGate.Shape[1];
    //                    using ITensor attnGateVector = backend.AllocateTensor("attn_gate_vec", DataType.F32, gateDim);
    //                    scalarBackend.QuantizedMatVecMul(norm1Output, block.AttnGate, attnGateVector);
    //                    scalarBackend.SiLU(attnGateVector);
    //                    scalarBackend.ElementWiseMul(attnContextVector, attnGateVector, attnContextVector);
    //                }

    //                if (block.AttnOut != null && block.AttnOut.Shape[0] == hiddenDim)
    //                {
    //                    using ITensor attnOutVector = backend.AllocateTensor("attn_out", DataType.F32, hiddenDim);
    //                    scalarBackend.QuantizedMatVecMul(attnContextVector, block.AttnOut, attnOutVector);
    //                    backend.Add(hiddenState, attnOutVector, hiddenState);
    //                }

    //                if (pos == 0 && layerIdx == 0)
    //                {
    //                    Console.WriteLine($"   -> After Layer 0 Attention [0..2]: [{hiddenSpan[0]:E4}, {hiddenSpan[1]:E4}, {hiddenSpan[2]:E4}]");
    //                }

    //                ITensor ffnNormWeight = block.FfnNorm ?? block.AttnNorm!;
    //                using ITensor norm2Output = backend.AllocateTensor("norm2", DataType.F32, hiddenDim);
    //                backend.RMSNorm(hiddenState, ffnNormWeight, norm2Output, 1e-6f);

    //                if (block.FfnGate != null && block.FfnUp != null && block.FfnDown != null)
    //                {
    //                    int ffnHiddenDim = block.FfnGate.Shape[1];
    //                    using ITensor gateVector = backend.AllocateTensor("gate", DataType.F32, ffnHiddenDim);
    //                    using ITensor upVector = backend.AllocateTensor("up", DataType.F32, ffnHiddenDim);
    //                    using ITensor swigluVector = backend.AllocateTensor("swiglu", DataType.F32, ffnHiddenDim);
    //                    using ITensor ffnOutVector = backend.AllocateTensor("ffn_out", DataType.F32, hiddenDim);

    //                    scalarBackend.QuantizedMatVecMul(norm2Output, block.FfnGate, gateVector);
    //                    scalarBackend.QuantizedMatVecMul(norm2Output, block.FfnUp, upVector);

    //                    scalarBackend.SiLU(gateVector);
    //                    scalarBackend.ElementWiseMul(gateVector, upVector, swigluVector);

    //                    scalarBackend.QuantizedMatVecMul(swigluVector, block.FfnDown, ffnOutVector);
    //                    backend.Add(hiddenState, ffnOutVector, hiddenState);
    //                }

    //                if (pos == 0 && layerIdx == 0)
    //                {
    //                    Console.WriteLine($"   -> After Layer 0 FFN [0..2]      : [{hiddenSpan[0]:E4}, {hiddenSpan[1]:E4}, {hiddenSpan[2]:E4}]");
    //                }
    //            }

    //            if (pos == 0 || pos == 21)
    //            {
    //                Console.WriteLine($"   -> Final Output Layer 63 [0..2]  : [{hiddenSpan[0]:E4}, {hiddenSpan[1]:E4}, {hiddenSpan[2]:E4}]");
    //            }
    //        }
    //    }
    //}

    //public static unsafe void TestLayer46()
    //{
    //    Console.WriteLine("======================================");
    //    Console.WriteLine("   Huldra Engine - Layer 46 Probe    ");
    //    Console.WriteLine("======================================");

    //    using IBackend backend = new ScalarBackend();
    //    // Please ensure this points to your local model path
    //    string modelPath = ModelPath;

    //    try
    //    {
    //        using var reader = new GgufReader(modelPath);
    //        Console.WriteLine("[Engine] Opening GGUF file and parsing headers...");
    //        reader.Parse();

    //        Console.WriteLine($"\n[Diagnostic] Inspecting All Tensors in Layer 46 (blk.46):");
    //        Console.WriteLine("--------------------------------------------------------------------------------");

    //        var layer46Tensors = reader.Tensors
    //            .Where(t => t.Name.StartsWith("blk.46.", StringComparison.OrdinalIgnoreCase))
    //            .ToList();

    //        if (layer46Tensors.Count == 0)
    //        {
    //            Console.WriteLine("Warning: No tensors found matching 'blk.46.'!");
    //        }

    //        foreach (var t in layer46Tensors)
    //        {
    //            string shapeStr = string.Join(" x ", t.Shape);
    //            Console.WriteLine($"  {t.Name,-40} | Type: {t.Type,-10} | Shape: [{shapeStr}]");
    //        }

    //        Console.WriteLine("--------------------------------------------------------------------------------");

    //        // Test Dequantizing a sample of weights from each tensor in Layer 46 to check for NaNs/Infs
    //        Console.WriteLine("\n[Diagnostic] Sampling first 3 values of each tensor in Layer 46:");
    //        var scalarBackend = (ScalarBackend)backend;

    //        foreach (var t in layer46Tensors)
    //        {
    //            int elemCount = (int)Math.Min(128, t.Shape.Aggregate(1L, (a, b) => a * (long)b));
    //            using ITensor buf = backend.AllocateTensor("probe_buf", DataType.F32, elemCount);
    //            Span<float> span = new Span<float>((void*)buf.DataPointer, elemCount);

    //            IntPtr ptr = reader.GetTensorDataPointer(t);
    //            using var tempTensor = new Tensor(t.Name, t.Type, [elemCount], ptr, t.MaxByteSize);

    //            try
    //            {
    //                scalarBackend.DequantizeTensor(tempTensor, span);
    //                Console.WriteLine($"  {t.Name,-40} -> First 3: [{span[0]:E4}, {span[1]:E4}, {span[2]:E4}]");
    //            }
    //            catch (Exception ex)
    //            {
    //                Console.WriteLine($"  {t.Name,-40} -> [DEQUANT ERROR]: {ex.Message}");
    //            }
    //        }
    //    }
    //    catch (Exception ex)
    //    {
    //        Console.ForegroundColor = ConsoleColor.Red;
    //        Console.WriteLine($"[Error] {ex.Message}");
    //        Console.ResetColor();
    //    }

    //    Console.WriteLine("\nPress any key to exit...");
    //    Console.ReadKey();
    //}
}
