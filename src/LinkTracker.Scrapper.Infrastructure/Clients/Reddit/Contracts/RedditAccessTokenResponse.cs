using System.Text.Json.Serialization;

namespace LinkTracker.Scrapper.Infrastructure.Clients.Reddit.Contracts;

internal sealed class RedditAccessTokenResponse
{
    [JsonPropertyName("access_token")] public string AccessToken { get; init; } = string.Empty;

    [JsonPropertyName("expires_in")] public int ExpiresInSeconds { get; init; }
}
