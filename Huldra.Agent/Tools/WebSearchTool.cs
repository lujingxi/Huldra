using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Nodes;
using HtmlAgilityPack;
using Huldra.Agent.Interfaces;

namespace Huldra.Agent.Tools;

/// <summary>
/// Tool to search the web using DuckDuckGo HTML Lite (No API Key required).
/// </summary>
public class WebSearchTool : IAgentTool
{
    private readonly HttpClient _httpClient;

    public WebSearchTool()
    {
        _httpClient = new HttpClient();
        // Disguise as a standard browser to prevent bot blocking
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
    }

    public string Name => "web_search";
    public string Description => "Searches the internet for up-to-date information. Use this to find documentation, resolve unknown errors, or get the latest news.";

    public JsonObject ParametersSchema => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["query"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "The search query (e.g., '.NET 10 new features' or 'how to center div css')"
            }
        },
        ["required"] = new JsonArray { "query" }
    };

    public async Task<string> ExecuteAsync(string argumentsJson, CancellationToken cancellationToken = default)
    {
        try
        {
            var docNode = JsonNode.Parse(argumentsJson);
            var query = docNode?["query"]?.ToString();
            if (string.IsNullOrWhiteSpace(query)) return "Error: 'query' parameter is missing.";

            var content = new FormUrlEncodedContent([new KeyValuePair<string, string>("q", query)]);
            var response = await _httpClient.PostAsync("https://html.duckduckgo.com/html/", content, cancellationToken);

            if (!response.IsSuccessStatusCode)
                return $"Error: Search engine responded with status code {response.StatusCode}.";

            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(html);

            var results = new StringBuilder();
            results.AppendLine($"Search Results for '{query}':\n");

            var nodes = htmlDoc.DocumentNode.SelectNodes("//div[contains(@class, 'result__body')]");
            if (nodes == null || nodes.Count == 0) return "No results found.";

            int count = 0;
            foreach (var node in nodes)
            {
                if (count >= 5) break; // Limit to top 5 results to save LLM context

                var titleNode = node.SelectSingleNode(".//h2/a");
                var snippetNode = node.SelectSingleNode(".//a[contains(@class, 'result__snippet')]");

                if (titleNode != null && snippetNode != null)
                {
                    string title = HtmlEntity.DeEntitize(titleNode.InnerText.Trim());
                    string url = titleNode.GetAttributeValue("href", "").Replace("//duckduckgo.com/l/?uddg=", "");
                    url = Uri.UnescapeDataString(url.Split('&')[0]); // Clean up DuckDuckGo redirect URL
                    string snippet = HtmlEntity.DeEntitize(snippetNode.InnerText.Trim());

                    results.AppendLine($"{count + 1}. {title}");
                    results.AppendLine($"   Snippet: {snippet}");
                    results.AppendLine($"   URL: {url}\n");
                    count++;
                }
            }

            return results.ToString();
        }
        catch (Exception ex)
        {
            return $"Error executing web_search: {ex.Message}";
        }
    }
}
