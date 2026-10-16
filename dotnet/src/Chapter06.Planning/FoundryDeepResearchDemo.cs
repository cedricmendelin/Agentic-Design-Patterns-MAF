using System.Net.Http.Json;
using System.Text.Json;
using AgenticDesignPatterns.Shared;

namespace Chapter06.Planning;

public static class FoundryDeepResearchDemo
{
    public const string SampleQuery =
        "Research the economic impact of semaglutide on global healthcare systems.";

    private const string ResearchInstructions =
        "You are a professional researcher preparing a structured, data-driven report. " +
        "Focus on reliable sources and include inline citations.";

    public static async Task RunAsync(
        FoundrySettings settings,
        string modelDeployment,
        string query = SampleQuery,
        Action<string>? writeOutput = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelDeployment);
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(settings.OpenAIEndpoint, "responses"))
        {
            Content = JsonContent.Create(new
            {
                model = modelDeployment,
                input = new object[]
                {
                    new
                    {
                        role = "developer",
                        content = new[] { new { type = "input_text", text = ResearchInstructions } }
                    },
                    new
                    {
                        role = "user",
                        content = new[] { new { type = "input_text", text = query } }
                    }
                },
                reasoning = new { summary = "auto" },
                tools = new[] { new { type = "web_search_preview" } }
            })
        };
        request.Headers.Add("api-key", settings.ApiKey);

        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Foundry Responses API returned HTTP {(int)response.StatusCode} ({response.StatusCode}).");
        }

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken);
        var outputText = new List<string>();
        writeOutput?.Invoke("\n--- Deep Research intermediate steps ---");

        if (document.RootElement.TryGetProperty("output", out var outputItems))
        {
            foreach (var item in outputItems.EnumerateArray())
            {
                var type = item.TryGetProperty("type", out var typeElement)
                    ? typeElement.GetString()
                    : null;
                if (type == "reasoning" &&
                    item.TryGetProperty("summary", out var summaryItems))
                {
                    foreach (var summary in summaryItems.EnumerateArray())
                    {
                        if (summary.TryGetProperty("text", out var text))
                        {
                            writeOutput?.Invoke($"Reasoning summary: {text.GetString()}");
                        }
                    }
                }
                else if (type == "web_search_call" &&
                    item.TryGetProperty("action", out var action))
                {
                    var queryText = action.TryGetProperty("query", out var searchQuery)
                        ? searchQuery.GetString()
                        : "query unavailable";
                    var status = item.TryGetProperty("status", out var statusElement)
                        ? statusElement.GetString()
                        : "unknown";
                    writeOutput?.Invoke($"Web search ({status}): {queryText}");
                }
                else if (type == "message" &&
                    item.TryGetProperty("content", out var contentItems))
                {
                    foreach (var content in contentItems.EnumerateArray())
                    {
                        if (content.TryGetProperty("type", out var contentType) &&
                            contentType.GetString() == "output_text" &&
                            content.TryGetProperty("text", out var text))
                        {
                            outputText.Add(text.GetString() ?? string.Empty);
                            if (content.TryGetProperty("annotations", out var annotations))
                            {
                                foreach (var annotation in annotations.EnumerateArray())
                                {
                                    if (annotation.TryGetProperty("url", out var url))
                                    {
                                        var title = annotation.TryGetProperty("title", out var titleElement)
                                            ? titleElement.GetString()
                                            : "Source";
                                        writeOutput?.Invoke($"Citation: {title} — {url.GetString()}");
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        var report = string.Join(Environment.NewLine, outputText.Where(text => !string.IsNullOrWhiteSpace(text)));
        if (string.IsNullOrWhiteSpace(report))
        {
            throw new InvalidOperationException("The Foundry Responses API returned no research report.");
        }

        writeOutput?.Invoke($"\n--- Deep Research report ---\n{report}");
    }
}
