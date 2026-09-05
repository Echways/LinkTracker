using System.Text.Json.Serialization;

namespace LinkTracker.Scrapper.Application.Clients.Reddit.Contracts;

public sealed class RedditListingEnvelope<T>
{
    [JsonPropertyName("data")] public RedditListingData<T> Data { get; init; } = new();
}
