namespace Huldra.Engine.Models;

public interface IContext
{
    void Evaluate(ReadOnlySpan<int> tokens);
    ReadOnlySpan<float> GetLogits();
}
