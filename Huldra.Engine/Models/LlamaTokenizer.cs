using System.Text;
using System.Text.RegularExpressions;

namespace Huldra.Engine.Models;

public sealed class LlamaTokenizer : ITokenizer
{
    private readonly Dictionary<string, int> _tokenToId = new();
    private readonly string[] _idToToken;
    private readonly Dictionary<(string, string), int> _merges = new();
    private readonly Regex _regex = new(@"'s|'t|'re|'ve|'m|'ll|'d| ?\p{L}+| ?\p{N}+| ?[^\s\p{L}\p{N}]+|\s+(?!\S)|\s+", RegexOptions.Compiled);

    // Store special tokens separately for priority matching
    private readonly Dictionary<string, int> _specialTokens = new();

    private readonly Dictionary<byte, char> _byteToChar;
    private readonly Dictionary<char, byte> _charToByte;
    private readonly HashSet<int> _endOfSequenceTokenIds = [];

    public IReadOnlySet<int> EndOfSequenceTokenIds => _endOfSequenceTokenIds;

    public LlamaTokenizer(Dictionary<string, object> metadata)
    {
        // Initialize byte mapping (Same as before)
        _byteToChar = new Dictionary<byte, char>();
        _charToByte = new Dictionary<char, byte>();
        var bs = new List<int>();
        for (int i = 33; i < 127; i++) bs.Add(i);
        for (int i = 161; i < 173; i++) bs.Add(i);
        for (int i = 174; i < 256; i++) bs.Add(i);
        int n = 0;
        for (int b = 0; b < 256; b++)
        {
            if (bs.Contains(b))
            {
                _byteToChar[(byte)b] = (char)b;
                _charToByte[(char)b] = (byte)b;
            }
            else
            {
                _byteToChar[(byte)b] = (char)(256 + n);
                _charToByte[(char)(256 + n)] = (byte)b;
                n++;
            }
        }

        // Load tokens
        if (!metadata.TryGetValue("tokenizer.ggml.tokens", out var tokensObj) || tokensObj is not object[] tokens)
            throw new InvalidOperationException("Tokenizer tokens not found in metadata.");

        _idToToken = new string[tokens.Length];
        for (int i = 0; i < tokens.Length; i++)
        {
            string token = (string)tokens[i];
            _idToToken[i] = token;
            _tokenToId[token] = i;
        }

        // Load merges
        if (metadata.TryGetValue("tokenizer.ggml.merges", out var mergesObj) && mergesObj is object[] merges)
        {
            for (int i = 0; i < merges.Length; i++)
            {
                string[] parts = ((string)merges[i]).Split(' ', 2);
                if (parts.Length == 2)
                {
                    _merges[(parts[0], parts[1])] = i;
                }
            }
        }

        // Load added tokens (Special Tokens)
        if (metadata.TryGetValue("tokenizer.ggml.added_tokens", out var addedObj) && addedObj is object[] addedTokens)
        {
            foreach (var token in addedTokens)
            {
                string t = (string)token;
                if (_tokenToId.TryGetValue(t, out int id))
                {
                    _specialTokens[t] = id;
                }
            }
        }

        // GGUF token_type marks control/user-defined tokens without requiring
        // model-family-specific string lists.
        if (metadata.TryGetValue("tokenizer.ggml.token_type", out var tokenTypesObj) &&
            tokenTypesObj is object[] tokenTypes)
        {
            int count = Math.Min(tokenTypes.Length, _idToToken.Length);
            for (int i = 0; i < count; i++)
            {
                int tokenType;
                try { tokenType = Convert.ToInt32(tokenTypes[i]); }
                catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException) { continue; }

                // 3 = control, 4 = user-defined, 5 = unused.
                if (tokenType is 3 or 4 or 5)
                    _specialTokens[_idToToken[i]] = i;
            }
        }

        // Prefer token ids supplied by GGUF metadata. Tokenizer code should not
        // contain model-family-specific hard-coded ids.
        if (metadata.TryGetValue("tokenizer.ggml.eos_token_id", out var eosIdObj))
        {
            try
            {
                int eosId = Convert.ToInt32(eosIdObj);
                if (eosId >= 0 && eosId < _idToToken.Length)
                    _endOfSequenceTokenIds.Add(eosId);
            }
            catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)
            {
                // Ignore malformed optional metadata; the exporter may provide
                // tokenizer.ggml.eos_token instead.
            }
        }

        if (metadata.TryGetValue("tokenizer.ggml.eos_token", out var eosTokenObj) &&
            eosTokenObj is string eosToken &&
            _tokenToId.TryGetValue(eosToken, out int eosTokenId))
        {
            _endOfSequenceTokenIds.Add(eosTokenId);
        }
    }

    public int? TryGetTokenId(string token) => _tokenToId.TryGetValue(token, out int id) ? id : null;

    public int Encode(string text, Span<int> outputTokens)
    {
        int count = 0;

        // 1. Match special tokens first
        int currentPos = 0;
        while (currentPos < text.Length)
        {
            int bestSpecialIdx = -1;
            int bestSpecialLen = 0;

            foreach (var kvp in _specialTokens)
            {
                int idx = text.IndexOf(kvp.Key, currentPos, StringComparison.Ordinal);
                if (idx == currentPos && kvp.Key.Length > bestSpecialLen)
                {
                    bestSpecialIdx = kvp.Value;
                    bestSpecialLen = kvp.Key.Length;
                }
            }

            // If a special token is found at current position
            if (bestSpecialIdx != -1)
            {
                if (count >= outputTokens.Length)
                    throw new ArgumentException("The output token buffer is too small.", nameof(outputTokens));

                outputTokens[count++] = bestSpecialIdx;
                currentPos += bestSpecialLen;
                continue;
            }

            // 2. Otherwise, apply standard BPE regex until next special token or end of string
            int nextSpecialPos = text.Length;
            foreach (var kvp in _specialTokens)
            {
                int idx = text.IndexOf(kvp.Key, currentPos, StringComparison.Ordinal);
                if (idx != -1 && idx < nextSpecialPos) nextSpecialPos = idx;
            }

            string chunk = text.Substring(currentPos, nextSpecialPos - currentPos);

            foreach (Match match in _regex.Matches(chunk))
            {
                string word = match.Value;
                byte[] bytes = Encoding.UTF8.GetBytes(word);
                string mapped = new string(bytes.Select(b => _byteToChar[b]).ToArray());

                List<string> symbols = mapped.Select(c => c.ToString()).ToList();
                while (symbols.Count > 1)
                {
                    int bestRank = int.MaxValue;
                    int bestIdx = -1;
                    for (int i = 0; i < symbols.Count - 1; i++)
                    {
                        if (_merges.TryGetValue((symbols[i], symbols[i + 1]), out int rank))
                        {
                            if (rank < bestRank)
                            {
                                bestRank = rank;
                                bestIdx = i;
                            }
                        }
                    }
                    if (bestIdx == -1) break;

                    symbols[bestIdx] += symbols[bestIdx + 1];
                    symbols.RemoveAt(bestIdx + 1);
                }

                foreach (string sym in symbols)
                {
                    if (_tokenToId.TryGetValue(sym, out int id))
                    {
                        if (count >= outputTokens.Length)
                            throw new ArgumentException("The output token buffer is too small.", nameof(outputTokens));

                        outputTokens[count++] = id;
                    }
                }
            }

            currentPos = nextSpecialPos;
        }

        return count;
    }

    public string Decode(ReadOnlySpan<int> tokens)
    {
        var bytes = new List<byte>();
        foreach (int id in tokens)
        {
            if (id < 0 || id >= _idToToken.Length) continue;
            string token = _idToToken[id];

            // Special tokens should be decoded directly as UTF8 string if they are not byte-mapped
            // For Qwen, special tokens like <|im_start|> are directly in the vocab
            if (_specialTokens.ContainsValue(id))
            {
                bytes.AddRange(Encoding.UTF8.GetBytes(token));
            }
            else
            {
                foreach (char c in token)
                {
                    if (_charToByte.TryGetValue(c, out byte b))
                    {
                        bytes.Add(b);
                    }
                }
            }
        }
        return Encoding.UTF8.GetString(bytes.ToArray());
    }
}
