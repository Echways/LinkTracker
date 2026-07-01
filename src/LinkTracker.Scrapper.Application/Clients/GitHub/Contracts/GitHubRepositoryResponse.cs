using System.Text.Json.Serialization;

namespace LinkTracker.Scrapper.Application.Clients.GitHub.Contracts;

public sealed class GitHubRepositoryResponse
{
    [JsonPropertyName("full_name")] public string FullName { get; init; } = string.Empty;

    [JsonPropertyName("html_url")] public Uri HtmlUrl { get; init; } = default!;

    [JsonPropertyName("updated_at")] public DateTimeOffset UpdatedAt { get; init; }
}