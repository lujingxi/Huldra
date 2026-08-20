using System.Diagnostics;
using System.Runtime.InteropServices;
using Huldra.Engine.Backends;
using Huldra.Engine.Models;
using Huldra.Engine.Sampling;

namespace Huldra.Cli;

internal sealed class BenchmarkRunner
{
    private const int ContextSize = 512;
    private const int MaxGeneratedTokens = 100;
    private const int TokenBufferSize = 4096;

    private readonly IModel _f16Model;
    private readonly IModel _q4Model;

    public BenchmarkRunner(
        IModel f16Model,
        IModel q4Model)
    {
        _f16Model = f16Model;
        _q4Model = q4Model;
    }

    public IReadOnlyList<BenchmarkResult> Run(string userPrompt)
    {
        BenchmarkCase[] cases =
        [
            new("Scalar", "F16", _f16Model),
            new("Vector", "F16", _f16Model),
            new("Scalar", "Q4_0", _q4Model),
            new("Vector", "Q4_0", _q4Model)
        ];

        var results = new List<BenchmarkResult>(cases.Length);

        Console.WriteLine(new string('=', 72));
        Console.WriteLine("Huldra CPU Benchmark");
        Console.WriteLine(new string('=', 72));
        Console.WriteLine($"Context size:        {ContextSize}");
        Console.WriteLine($"Max generated:      {MaxGeneratedTokens} tokens");
        Console.WriteLine("Sampling:            Greedy");
        Console.WriteLine();

        foreach (BenchmarkCase benchmarkCase in cases)
        {
            BenchmarkResult result = RunCase(
                benchmarkCase,
                userPrompt);

            results.Add(result);
        }

        return results;
    }

    private static BenchmarkResult RunCase(
        BenchmarkCase benchmarkCase,
        string userPrompt)
    {
        IBackend backend =
            BackendRuntime.Instance.GetBackend(benchmarkCase.Backend);

        IModel model = benchmarkCase.Model;
        string prompt = model.ApplyChatTemplate(userPrompt);

        int[] tokenBuffer = new int[TokenBufferSize];

        int tokenCount =
            model.Tokenizer.Encode(
                prompt,
                tokenBuffer);

        if (tokenCount <= 0)
        {
            throw new InvalidOperationException(
                "Tokenizer produced no tokens for the benchmark prompt.");
        }

        if (tokenCount >= ContextSize)
        {
            throw new InvalidOperationException(
                $"Prompt contains {tokenCount} tokens, " +
                $"which exceeds the benchmark context size of {ContextSize}.");
        }

        List<int> tokens =
            new(tokenCount + MaxGeneratedTokens);

        for (int i = 0; i < tokenCount; i++)
        {
            tokens.Add(tokenBuffer[i]);
        }

        // Keep generated tokens separately from the full context.
        //
        // "tokens" contains:
        //   prompt + generated tokens
        //
        // "generatedTokenIds" contains:
        //   generated tokens only
        //
        // This lets us decode the actual model output without
        // accidentally decoding the prompt/chat template again.
        List<int> generatedTokenIds =
            new(MaxGeneratedTokens);

        Console.WriteLine(new string('-', 72));
        Console.WriteLine(
            $"{benchmarkCase.Backend} + {benchmarkCase.Format}");

        Console.WriteLine(
            $"Prompt tokens: {tokenCount}");

        /*
         * Every benchmark gets a completely fresh context.
         *
         * The model itself is reused, but the KV cache and inference state
         * are not shared between benchmark cases.
         */
        IContext context =
            model.CreateContext(
                ContextSize,
                backend);

        /*
         * Use greedy sampling for benchmarking.
         *
         * This avoids randomness between benchmark runs and avoids the
         * full vocabulary sort performed by the normal temperature/top-k/
         * top-p sampler.
         */
        var sampler = new Sampler(
            new SamplerConfig
            {
                Temperature = 0f,
                TopK = 1,
                TopP = 1f
            });

        int generatedTokens = 0;

        Stopwatch stopwatch = Stopwatch.StartNew();

        using (BackendParallelInstrumentation.Begin())
        {
            for (int i = 0; i < MaxGeneratedTokens; i++)
            {
                ReadOnlySpan<int> inputTokens;

                if (i == 0)
                {
                    inputTokens =
                        CollectionsMarshal.AsSpan(tokens)
                            .Slice(0, tokenCount);
                }
                else
                {
                    inputTokens =
                        CollectionsMarshal.AsSpan(tokens)
                            .Slice(tokens.Count - 1, 1);
                }

                context.Evaluate(inputTokens);

                ReadOnlySpan<float> logits =
                    context.GetLogits();

                int nextToken =
                    sampler.Sample(logits);

                if (model.Tokenizer.EndOfSequenceTokenIds.Contains(nextToken))
                    break;

                tokens.Add(nextToken);
                generatedTokens++;
            }
        }

        stopwatch.Stop();

        BackendParallelStats? parallelStats =
            BackendParallelInstrumentation.LastStats;

        double elapsedSeconds =
            stopwatch.Elapsed.TotalSeconds;

        double tokensPerSecond =
            elapsedSeconds > 0.0
                ? generatedTokens / elapsedSeconds
                : 0.0;

        Console.WriteLine(
            $"Generated: {generatedTokens} tokens");

        Console.WriteLine(
            $"Elapsed: {elapsedSeconds:F3} s");

        Console.WriteLine(
            $"Throughput: {tokensPerSecond:F2} tok/s");

        if (parallelStats is not null)
        {
            Console.WriteLine(
                $"MatMul parallelism: " +
                $"workers={parallelStats.WorkerCount}, " +
                $"max-concurrent={parallelStats.MaxConcurrentWorkers}, " +
                $"threads={parallelStats.DistinctManagedThreads}, " +
                $"logical-processors={parallelStats.DistinctLogicalProcessors}");
        }

        string output = model.Tokenizer.Decode(
            CollectionsMarshal.AsSpan(generatedTokenIds));

        Console.WriteLine("Output:");

        if (string.IsNullOrEmpty(output))
        {
            Console.WriteLine("(empty)");
        }
        else
        {
            Console.WriteLine(output);
        }

        return new BenchmarkResult(
            benchmarkCase.Backend,
            benchmarkCase.Format,
            tokenCount,
            generatedTokens,
            stopwatch.Elapsed,
            tokensPerSecond,
            output);
    }

    public static void PrintSummary(
        IReadOnlyList<BenchmarkResult> results)
    {
        Console.WriteLine();
        Console.WriteLine(new string('=', 72));
        Console.WriteLine("Benchmark Summary");
        Console.WriteLine(new string('=', 72));

        Console.WriteLine(
            $"{"Backend",-10}" +
            $"{"Format",-8}" +
            $"{"Prompt",8}" +
            $"{"Generated",10}" +
            $"{"Time",12}" +
            $"{"tok/s",12}");

        Console.WriteLine(new string('-', 72));

        foreach (BenchmarkResult result in results)
        {
            Console.WriteLine(
                $"{result.Backend,-10}" +
                $"{result.Format,-8}" +
                $"{result.PromptTokens,8}" +
                $"{result.GeneratedTokens,10}" +
                $"{result.Elapsed.TotalSeconds,11:F3}s" +
                $"{result.TokensPerSecond,12:F2}");
        }

        Console.WriteLine(new string('=', 72));
    }

    private sealed record BenchmarkCase(
        string Backend,
        string Format,
        IModel Model);
}

internal sealed record BenchmarkResult(
    string Backend,
    string Format,
    int PromptTokens,
    int GeneratedTokens,
    TimeSpan Elapsed,
    double TokensPerSecond,
    string Output);