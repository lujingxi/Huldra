using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Huldra.Agent.Services;

/// <summary>
/// A highly resilient JSON extractor designed to handle erratic LLM outputs.
/// </summary>
public static class RobustJsonParser
{
    private static readonly JsonSerializerOptions DefaultOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    /// <summary>
    /// Attempts to find and deserialize a JSON block from any messy text.
    /// </summary>
    public static T? ExtractJson<T>(string rawText) where T : class
    {
        if (string.IsNullOrWhiteSpace(rawText)) return null;

        try
        {
            // 1. Try to extract content inside markdown blocks ```json ... ```
            var markdownRegex = new Regex(@"```json\s*([\s\S]*?)\s*```", RegexOptions.IgnoreCase);
            var markdownMatch = markdownRegex.Match(rawText);

            if (markdownMatch.Success)
            {
                var candidate = markdownMatch.Groups[1].Value.Trim();
                var result = TryDeserialize<T>(candidate);
                if (result != null) return result;
            }

            // 2. Fallback: Find the outermost curly braces { ... }
            var braceRegex = new Regex(@"({[\s\S]*})", RegexOptions.Compiled);
            var braceMatch = braceRegex.Match(rawText);

            if (braceMatch.Success)
            {
                var candidate = braceMatch.Groups[1].Value.Trim();
                var result = TryDeserialize<T>(candidate);
                if (result != null) return result;
            }

            // 3. Fallback: Try deserializing the raw text directly
            return JsonSerializer.Deserialize<T>(rawText, DefaultOptions);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[RobustJsonParser Warning] All extraction strategies failed: {ex.Message}");
            return null;
        }
    }

    private static T? TryDeserialize<T>(string jsonCandidate) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(jsonCandidate, DefaultOptions);
        }
        catch
        {
            return null;
        }
    }
}

