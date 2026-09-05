using System.Text.Json.Serialization;

namespace LinkTracker.Scrapper.Application.Clients.Reddit.Contracts;

public sealed class RedditListingData<T>
{
    [JsonPropertyName("children")] public IReadOnlyList<RedditThingResponse<T>> Children { get; init; } = [];
}
