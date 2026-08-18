namespace Huldra.Engine.Models;

public interface ITokenizer
{
    int Encode(string text, Span<int> outputTokens);
    string Decode(ReadOnlySpan<int> tokens);
    int? TryGetTokenId(string token);
    IReadOnlySet<int> EndOfSequenceTokenIds { get; }
}
