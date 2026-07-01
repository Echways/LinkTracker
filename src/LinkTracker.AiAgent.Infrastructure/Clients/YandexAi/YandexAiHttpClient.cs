using System.Net.Http.Headers;
using System.Net.Http.Json;
using LinkTracker.AiAgent.Application.Abstractions;
using LinkTracker.AiAgent.Infrastructure.Clients.YandexAi.Contracts;
using LinkTracker.AiAgent.Infrastructure.Configuration.AiAgent;
using LinkTracker.AiAgent.Infrastructure.Configuration.YandexAi;
using LinkTracker.Shared.Constants;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LinkTracker.AiAgent.Infrastructure.Clients.YandexAi;

internal sealed class YandexAiHttpClient(
    IHttpClientFactory httpClientFactory,
    IOptions<YandexAiOptions> yandexOptions,
    IOptions<AiAgentOptions> agentOptions,
    ILogger<YandexAiHttpClient> logger) : ILinkUpdateSummarizer
{
    public async Task<string> SummarizeAsync(string text, CancellationToken ct)
    {
        var threshold = agentOptions.Value.Summarization.Threshold;

        if (text.Length <= threshold)
        {
            return text;
        }

        try
        {
            return await CallApiAsync(text, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Yandex AI суммаризация завершилась ошибкой ({Type}). Используется заглушка.", ex.GetType().Name);
            return FallbackTruncate(text, threshold);
        }
    }

    private async Task<string> CallApiAsync(string text, CancellationToken ct)
    {
        var opts = yandexOptions.Value;

        var requestBody = new YandexResponsesRequest { Model = $"gpt://{opts.FolderId}/{opts.ModelId}/latest", Instructions = "You are a concise summarizer. Summarize the given update in 2-3 sentences.", Input = text };

        var httpClient = httpClientFactory.CreateClient(nameof(YandexAiHttpClient));

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/responses");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Api-Key", opts.ApiKey);
        httpRequest.Headers.Add("OpenAI-Project", opts.FolderId);
        httpRequest.Content = JsonContent.Create(requestBody);

        var response = await httpClient.SendAsync(httpRequest, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<YandexResponsesResponse>(ct);

        var responseText = result?.Output?.FirstOrDefault()?.Content?.FirstOrDefault()?.Text;

        if (!string.IsNullOrWhiteSpace(responseText))
        {
            return responseText;
        }

        logger.LogWarning("Yandex AI вернул пустой ответ.");
        return text;
    }

    private static string FallbackTruncate(string text, int threshold)
    {
        var markerIndex = text.IndexOf(SystemMessageMarkers.FailedLinkReport, StringComparison.Ordinal);

        if (markerIndex < 0)
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

        var mainContent = text[..markerIndex].TrimEnd();
        var systemSuffix = text[markerIndex..];

        if (mainContent.Length <= threshold)
        {
            return text;
        }

        var mainCutAt = threshold;
        while (mainCutAt > 0 && mainContent[mainCutAt - 1] != '\n')
        {
            mainCutAt--;
        }

        if (mainCutAt == 0)
        {
            mainCutAt = threshold;
        }

        return string.Concat(mainContent.AsSpan(0, mainCutAt).TrimEnd(), "\n...\n", systemSuffix);
    }
}