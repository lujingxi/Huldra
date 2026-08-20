using System.Text;
using Huldra.Engine.Models;

namespace Huldra.Cli;

internal static class Program
{
    private const string F16ModelPath =
        @"C:\Users\lujin\.lmstudio\models\Qwen\Qwen2.5-0.5B-Instruct-GGUF\qwen2.5-0.5b-instruct-fp16.gguf";

    private const string Q4_0ModelPath =
        @"C:\Users\lujin\.lmstudio\models\Qwen\Qwen2.5-0.5B-Instruct-GGUF\qwen2.5-0.5b-instruct-q4_0.gguf";

    public static void Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;

        Console.WriteLine("Huldra CLI");
        Console.WriteLine();

        Console.Write("User: ");
        string input = Console.ReadLine() ?? "Hello";

        if (string.IsNullOrWhiteSpace(input))
        {
            input = "Hello";
        }

        Console.WriteLine();
        Console.WriteLine("Loading F16 model...");

        IModel f16Model = ModelFactory.Load(F16ModelPath);

        Console.WriteLine("F16 model loaded.");
        Console.WriteLine();

        Console.WriteLine("Loading Q4_0 model...");

        IModel q4Model = ModelFactory.Load(Q4_0ModelPath);

        Console.WriteLine("Q4_0 model loaded.");
        Console.WriteLine();

        var runner = new BenchmarkRunner(
            f16Model,
            q4Model);

        IReadOnlyList<BenchmarkResult> results = runner.Run(input);

        BenchmarkRunner.PrintSummary(results);

        Console.WriteLine();
        Console.WriteLine("Benchmark finished.");
    }
}
