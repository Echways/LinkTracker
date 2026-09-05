using System.Text.Json.Serialization;

namespace LinkTracker.Scrapper.Application.Clients.Reddit.Contracts;

public sealed class RedditPostResponse
{
    [JsonPropertyName("id")] public string Id { get; init; } = string.Empty;

    [JsonPropertyName("title")] public string Title { get; init; } = string.Empty;

    [JsonPropertyName("selftext")] public string Selftext { get; init; } = string.Empty;

    [JsonPropertyName("author")] public string Author { get; init; } = string.Empty;

    [JsonPropertyName("permalink")] public string Permalink { get; init; } = string.Empty;

    [JsonPropertyName("created_utc")] public double CreatedUtcSeconds { get; init; }

    public DateTimeOffset CreatedAt => DateTimeOffset.FromUnixTimeSeconds((long)CreatedUtcSeconds);
}
