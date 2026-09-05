using System.Text.Json.Serialization;

namespace LinkTracker.Scrapper.Application.Clients.Reddit.Contracts;

public sealed class RedditThingResponse<T>
{
    [JsonPropertyName("kind")] public string Kind { get; init; } = string.Empty;

    [JsonPropertyName("data")] public T Data { get; init; } = default!;
}
