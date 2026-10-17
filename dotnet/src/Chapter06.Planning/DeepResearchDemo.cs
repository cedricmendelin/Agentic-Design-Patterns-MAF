using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using AgenticDesignPatterns.Shared;

namespace Chapter06.Planning;

public static class DeepResearchDemo
{
    private static readonly TimeSpan MaximumWait = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(3);

    public static async Task RunAsync(
        FoundrySettings settings,
        string deploymentAlias,
        string researchQuestion,
        Action<string>? writeOutput = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentException.ThrowIfNullOrWhiteSpace(deploymentAlias);
        ArgumentException.ThrowIfNullOrWhiteSpace(researchQuestion);
        cancellationToken.ThrowIfCancellationRequested();

        var deployment = settings.ResolveModelDeployment(deploymentAlias);
        var endpoint = new Uri(settings.OpenAIEndpoint, "responses");
        using var client = new HttpClient();
        using var response = await SendRequestAsync(
            client,
            endpoint,
            HttpMethod.Post,
            settings.ApiKey,
            new
            {
                model = deployment,
                background = true,
                reasoning = new { summary = "auto" },
                tools = new[] { new { type = "web_search_preview" } },
                input = new object[]
                {
                    new
                    {
                        role = "developer",
                        content = new[]
                        {
                            new
                            {
                                type = "input_text",
                                text = "You are a professional researcher preparing a structured, data-driven report. Focus on data-rich insights, use reliable sources, and include inline citations."
                            }
                        }
                    },
                    new
                    {
                        role = "user",
                        content = new[]
                        {
                            new { type = "input_text", text = researchQuestion }
                        }
                    }
                }
            },
            cancellationToken);

        using var initialDocument = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);
        var responseId = initialDocument.RootElement.TryGetProperty("id", out var idElement)
            ? idElement.GetString()
            : null;
        if (string.IsNullOrWhiteSpace(responseId))
        {
            throw new InvalidOperationException("Foundry did not return a response ID for the research task.");
        }

        writeOutput?.Invoke($"Research task started with deployment '{deployment}'.");
        var initialStatus = initialDocument.RootElement.TryGetProperty("status", out var statusElement)
            ? statusElement.GetString()
            : null;
        if (string.Equals(initialStatus, "completed", StringComparison.OrdinalIgnoreCase))
        {
            PrintResponse(initialDocument.RootElement, writeOutput);
            return;
        }

        if (initialStatus is not ("queued" or "in_progress"))
        {
            throw new InvalidOperationException(
                $"Foundry Deep Research returned unexpected status '{initialStatus ?? "unknown"}'.");
        }

        var responseEndpoint = new Uri(settings.OpenAIEndpoint, $"responses/{Uri.EscapeDataString(responseId)}");
        JsonDocument completedDocument;
        try
        {
            completedDocument = await PollUntilCompleteAsync(
                client,
                responseEndpoint,
                settings.ApiKey,
                initialStatus,
                cancellationToken);
        }
        catch (Exception exception) when (exception is TimeoutException or OperationCanceledException)
        {
            await CancelResponseAsync(client, responseEndpoint, settings.ApiKey, exception, writeOutput);
            throw;
        }

        using (completedDocument)
        {
            PrintResponse(completedDocument.RootElement, writeOutput);
        }
    }

    private static async Task CancelResponseAsync(
        HttpClient client,
        Uri responseEndpoint,
        string apiKey,
        Exception originalFailure,
        Action<string>? writeOutput)
    {
        using var cleanupTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            using var response = await SendRequestAsync(
                client,
                new Uri(responseEndpoint.AbsoluteUri + "/cancel"),
                HttpMethod.Post,
                apiKey,
                payload: null,
                cleanupTimeout.Token);
        }
        catch (Exception cleanupFailure)
        {
            originalFailure.Data["DeepResearchCancellationFailure"] = cleanupFailure;
            writeOutput?.Invoke(
                $"Failed to cancel the unfinished Deep Research response: {cleanupFailure.Message}");
        }
    }

    private static async Task<HttpResponseMessage> SendRequestAsync(
        HttpClient client,
        Uri endpoint,
        HttpMethod method,
        string apiKey,
        object? payload,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, endpoint);
        request.Headers.TryAddWithoutValidation("api-key", apiKey);
        if (payload is not null)
        {
            request.Content = JsonContent.Create(payload);
        }

        var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var statusCode = response.StatusCode;
            response.Dispose();
            throw new InvalidOperationException(
                $"Foundry Responses API returned HTTP {(int)statusCode} ({statusCode}). " +
                "Verify the Deep Research deployment, API access, and web-search availability.");
        }

        return response;
    }

    private static async Task<JsonDocument> PollUntilCompleteAsync(
        HttpClient client,
        Uri responseEndpoint,
        string apiKey,
        string? initialStatus,
        CancellationToken cancellationToken)
    {
        var timer = Stopwatch.StartNew();
        var status = initialStatus;
        while (string.Equals(status, "queued", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(status, "in_progress", StringComparison.OrdinalIgnoreCase))
        {
            if (timer.Elapsed >= MaximumWait)
            {
                throw new TimeoutException(
                    $"The Foundry Deep Research task did not complete within {MaximumWait.TotalMinutes:0} minutes.");
            }

            await Task.Delay(PollInterval, cancellationToken);
            using var response = await SendRequestAsync(
                client,
                responseEndpoint,
                HttpMethod.Get,
                apiKey,
                payload: null,
                cancellationToken);
            var document = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken);
            status = document.RootElement.TryGetProperty("status", out var statusElement)
                ? statusElement.GetString()
                : null;

            if (string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase))
            {
                return document;
            }

            if (status is not ("queued" or "in_progress"))
            {
                var error = document.RootElement.TryGetProperty("error", out var errorElement)
                    ? errorElement.ToString()
                    : "No error details were returned.";
                document.Dispose();
                throw new InvalidOperationException(
                    $"Foundry Deep Research ended with status '{status ?? "unknown"}': {error}");
            }

            document.Dispose();
        }

        if (string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Foundry completed the Deep Research task before the first poll but returned no final response.");
        }

        throw new InvalidOperationException(
            $"Foundry Deep Research returned unexpected status '{status ?? "unknown"}'.");
    }

    private static void PrintResponse(JsonElement response, Action<string>? writeOutput)
    {
        var outputText = new List<string>();
        var citations = new List<(string Title, string Url, int Start, int End)>();
        var searchCalls = new List<(string Query, string Status)>();
        var reasoningSummaries = new List<string>();

        if (response.TryGetProperty("output", out var output) && output.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in output.EnumerateArray())
            {
                var type = GetString(item, "type");
                if (type == "message" &&
                    item.TryGetProperty("content", out var content) &&
                    content.ValueKind == JsonValueKind.Array)
                {
                    foreach (var part in content.EnumerateArray())
                    {
                        if (GetString(part, "type") == "output_text")
                        {
                            var text = GetString(part, "text");
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                outputText.Add(text);
                            }

                            if (part.TryGetProperty("annotations", out var annotations) &&
                                annotations.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var annotation in annotations.EnumerateArray())
                                {
                                    if (GetString(annotation, "type") != "url_citation")
                                    {
                                        continue;
                                    }

                                    citations.Add((
                                        GetString(annotation, "title") ?? "Untitled source",
                                        GetString(annotation, "url") ?? string.Empty,
                                        GetInt32(annotation, "start_index"),
                                        GetInt32(annotation, "end_index")));
                                }
                            }
                        }
                    }
                }
                else if (type == "web_search_call")
                {
                    var action = item.TryGetProperty("action", out var actionElement)
                        ? actionElement
                        : default;
                    searchCalls.Add((
                        GetString(action, "query") ?? "Query not provided",
                        GetString(item, "status") ?? "unknown"));
                }
                else if (type == "reasoning" &&
                         item.TryGetProperty("summary", out var summary) &&
                         summary.ValueKind == JsonValueKind.Array)
                {
                    reasoningSummaries.AddRange(
                        summary.EnumerateArray()
                            .Select(part => GetString(part, "text"))
                                .OfType<string>()
                                .Where(text => !string.IsNullOrWhiteSpace(text)));
                }
            }
        }

        var report = string.Join(Environment.NewLine, outputText);
        if (string.IsNullOrWhiteSpace(report))
        {
            throw new InvalidOperationException("Foundry completed Deep Research without a final report.");
        }

        writeOutput?.Invoke("\n--- Deep Research report ---");
        writeOutput?.Invoke(report);
        writeOutput?.Invoke("\n--- Citations ---");
        if (citations.Count == 0)
        {
            writeOutput?.Invoke("No citation annotations were returned.");
        }
        else
        {
            foreach (var (citation, index) in citations.Select((citation, index) => (citation, index + 1)))
            {
                writeOutput?.Invoke(
                    $"{index}. {citation.Title} — {citation.Url} " +
                    $"(characters {citation.Start}-{citation.End})");
            }
        }

        writeOutput?.Invoke("\n--- Intermediate steps ---");
        foreach (var (call, index) in searchCalls.Select((call, index) => (call, index + 1)))
        {
            writeOutput?.Invoke($"Web search {index}: \"{call.Query}\" ({call.Status})");
        }

        foreach (var (summary, index) in reasoningSummaries.Select((summary, index) => (summary, index + 1)))
        {
            writeOutput?.Invoke($"Reasoning summary {index}: {summary}");
        }

        if (searchCalls.Count == 0 && reasoningSummaries.Count == 0)
        {
            writeOutput?.Invoke("No intermediate steps were returned.");
        }
    }

    private static string? GetString(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int GetInt32(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(property, out var value) &&
        value.TryGetInt32(out var result)
            ? result
            : 0;
}
