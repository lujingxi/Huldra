using HtmlAgilityPack;
using Huldra.Agent.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Nodes;

namespace Huldra.Agent.Tools;

/// <summary>
/// Tool to fetch and extract raw text from a specific webpage.
/// </summary>
public class FetchWebpageTool : IAgentTool
{
    private readonly HttpClient _httpClient;

    public FetchWebpageTool()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
    }

    public string Name => "fetch_webpage";
    public string Description => "Reads the text content of a specific webpage URL. Useful for reading official documentation or GitHub issues found via web_search.";

    public JsonObject ParametersSchema => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["url"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "The exact URL to fetch (must start with http:// or https://)"
            }
        },
        ["required"] = new JsonArray { "url" }
    };

    public async Task<string> ExecuteAsync(string argumentsJson, CancellationToken cancellationToken = default)
    {
        try
        {
            var docNode = JsonNode.Parse(argumentsJson);
            var url = docNode?["url"]?.ToString();
            if (string.IsNullOrWhiteSpace(url)) return "Error: 'url' parameter is missing.";

            var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode) return $"Error: Could not fetch URL (Status: {response.StatusCode}).";

            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(html);

            // Remove noise (scripts, styles, nav, footers)
            var nodesToRemove = htmlDoc.DocumentNode.SelectNodes("//script | //style | //nav | //footer | //header | //noscript");
            if (nodesToRemove != null)
            {
                foreach (var node in nodesToRemove) node.Remove();
            }

            string text = HtmlEntity.DeEntitize(htmlDoc.DocumentNode.InnerText);

            // Normalize whitespace
            var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                            .Select(l => l.Trim())
                            .Where(l => l.Length > 0);

            var cleanText = string.Join("\n", lines);

            // Truncate to ~10,000 characters to prevent local LLM context overflow
            int maxLength = 10000;
            if (cleanText.Length > maxLength)
            {
                cleanText = string.Concat(cleanText.AsSpan(0, maxLength), "\n\n...[Content Truncated due to length]...");
            }

            return $"--- Content of {url} ---\n{cleanText}\n--- End of Webpage ---";
        }
        catch (Exception ex)
        {
            return $"Error fetching webpage: {ex.Message}";
        }
    }
}
