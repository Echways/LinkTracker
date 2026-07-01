using System.Text.Json.Serialization;

namespace LinkTracker.AiAgent.Infrastructure.Clients.YandexAi.Contracts;

internal sealed class YandexResponsesRequest
{
    [JsonPropertyName("model")] public string Model { get; init; } = string.Empty;

    [JsonPropertyName("instructions")] public string Instructions { get; init; } = string.Empty;

    [JsonPropertyName("input")] public string Input { get; init; } = string.Empty;

    [JsonPropertyName("stream")] public bool Stream { get; init; } = false;
}