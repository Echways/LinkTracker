using System.Net.Http.Headers;
using System.Net.Http.Json;
using LinkTracker.AiAgent.Application.Abstractions;
using LinkTracker.AiAgent.Application.Telemetry.Abstractions;
using LinkTracker.AiAgent.Infrastructure.Clients.YandexAi.Contracts;
using LinkTracker.AiAgent.Infrastructure.Configuration.AiAgent;
using LinkTracker.AiAgent.Infrastructure.Configuration.YandexAi;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LinkTracker.AiAgent.Infrastructure.Clients.YandexAi;

internal sealed class YandexAiHttpClient(
    IHttpClientFactory httpClientFactory,
    IOptions<YandexAiOptions> yandexOptions,
    IOptions<AiAgentOptions> agentOptions,
    IAiAgentMetrics metrics,
    ILogger<YandexAiHttpClient> logger) : ILinkUpdateSummarizer
{
    private const string Instructions = "You are a concise summarizer. Summarize the given update in 2-3 sentences. Always answer in Russian.";

    public async Task<string> SummarizeAsync(string text, CancellationToken ct)
    {
        var threshold = agentOptions.Value.Summarization.Threshold;

        if (text.Length <= threshold)
        {
            return text;
        }

        try
        {
            var summary = await CallApiAsync(text, ct);

            if (string.IsNullOrWhiteSpace(summary))
            {
                logger.LogWarning("Yandex AI returned an empty response, falling back to text truncation.");
                metrics.IncrementSummarizationFallback("empty_response");

                return FallbackTruncate(text, threshold);
            }

            metrics.IncrementSummarization();
            return summary;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Yandex AI summarization failed ({Type}), falling back to text truncation.", ex.GetType().Name);
            metrics.IncrementSummarizationFallback(ex.GetType().Name);

            return FallbackTruncate(text, threshold);
        }
    }

    private async Task<string?> CallApiAsync(string text, CancellationToken ct)
    {
        var opts = yandexOptions.Value;

        var requestBody = new YandexResponsesRequest { Model = $"gpt://{opts.FolderId}/{opts.ModelId}/latest", Instructions = Instructions, Input = text };

        var httpClient = httpClientFactory.CreateClient(nameof(YandexAiHttpClient));

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/responses");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Api-Key", opts.ApiKey);
        httpRequest.Headers.Add("OpenAI-Project", opts.FolderId);
        httpRequest.Content = JsonContent.Create(requestBody);

        var response = await httpClient.SendAsync(httpRequest, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<YandexResponsesResponse>(ct);

        return result?.Output?.FirstOrDefault()?.Content?.FirstOrDefault()?.Text;
    }

    private static string FallbackTruncate(string text, int threshold)
    {
        var cutAt = threshold;

        while (cutAt > 0 && text[cutAt - 1] != '\n')
        {
            cutAt--;
        }

        if (cutAt == 0)
        {
            cutAt = threshold;
        }

        return string.Concat(text.AsSpan(0, cutAt).TrimEnd(), "\n...");
    }
}
