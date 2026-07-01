using System.Text.Json.Serialization;

namespace LinkTracker.Scrapper.Application.Clients.GitHub.Contracts;

public sealed class GitHubIssueResponse
{
    [JsonPropertyName("id")] public long Id { get; init; }

    [JsonPropertyName("title")] public string Title { get; init; } = string.Empty;

    [JsonPropertyName("body")] public string? Body { get; init; }

    [JsonPropertyName("created_at")] public DateTimeOffset CreatedAt { get; init; }

    [JsonPropertyName("html_url")] public Uri HtmlUrl { get; init; } = default!;

    [JsonPropertyName("user")] public GitHubUserResponse? User { get; init; }

    [JsonPropertyName("pull_request")] public object? PullRequest { get; init; }
}