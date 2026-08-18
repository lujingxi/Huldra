namespace Huldra.Engine.Sampling;

public sealed class Sampler(SamplerConfig config)
{
    private readonly SamplerConfig _config = config;
    private readonly Random _rng = new();

    public int Sample(ReadOnlySpan<float> logits)
    {
        int vocabSize = logits.Length;

        // 1. Apply Temperature
        // If temperature is near 0, just do greedy argmax
        if (_config.Temperature <= 0.01f)
        {
            return Argmax(logits);
        }

        // Find max logit for numerical stability
        float maxLogit = float.MinValue;
        for (int i = 0; i < vocabSize; i++)
        {
            if (logits[i] > maxLogit) maxLogit = logits[i];
        }

        // Calculate probabilities and sum
        float[] probs = new float[vocabSize];
        float sum = 0f;
        for (int i = 0; i < vocabSize; i++)
        {
            // logit / temp -> exp -> softmax
            float val = MathF.Exp((logits[i] - maxLogit) / _config.Temperature);
            probs[i] = val;
            sum += val;
        }

        // Normalize
        for (int i = 0; i < vocabSize; i++) probs[i] /= sum;

        // 2. Top-K & Top-P Filtering
        // Sort indices by probability descending
        int[] indices = new int[vocabSize];
        for (int i = 0; i < vocabSize; i++) indices[i] = i;

        // Partial sort is better, but for simplicity we do full sort.
        // Since this runs only once per token generation, the overhead is acceptable.
        Array.Sort(indices, (a, b) => probs[b].CompareTo(probs[a]));

        int k = Math.Min(_config.TopK, vocabSize);

        // Calculate cumulative probability for Top-P
        float cumProb = 0f;
        int lastIdx = 0;
        for (int i = 0; i < k; i++)
        {
            cumProb += probs[indices[i]];
            lastIdx = i;
            if (cumProb >= _config.TopP) break;
        }

        // 3. Multinomial Sampling
        float r = _rng.NextSingle() * cumProb;
        float acc = 0f;
        for (int i = 0; i <= lastIdx; i++)
        {
            acc += probs[indices[i]];
            if (r <= acc) return indices[i];
        }

        return indices[lastIdx];
    }

    private static int Argmax(ReadOnlySpan<float> logits)
    {
        int bestIdx = 0;
        float maxVal = float.MinValue;
        for (int i = 0; i < logits.Length; i++)
        {
            if (logits[i] > maxVal)
            {
                maxVal = logits[i];
                bestIdx = i;
            }
        }
        return bestIdx;
    }
}
