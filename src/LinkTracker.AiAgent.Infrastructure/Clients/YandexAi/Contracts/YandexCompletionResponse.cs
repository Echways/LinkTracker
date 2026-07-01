using System.Text.Json.Serialization;

namespace LinkTracker.AiAgent.Infrastructure.Clients.YandexAi.Contracts;

internal sealed class YandexResponsesResponse
{
    [JsonPropertyName("output")] public IReadOnlyList<YandexOutputBlock>? Output { get; init; }
}

internal sealed class YandexOutputBlock
{
    [JsonPropertyName("content")] public IReadOnlyList<YandexContentBlock>? Content { get; init; }
}

internal sealed class YandexContentBlock
{
    [JsonPropertyName("text")] public string? Text { get; init; }
}