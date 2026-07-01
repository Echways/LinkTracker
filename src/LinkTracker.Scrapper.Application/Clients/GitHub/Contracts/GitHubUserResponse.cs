using System.Text.Json.Serialization;

namespace LinkTracker.Scrapper.Application.Clients.GitHub.Contracts;

public sealed class GitHubUserResponse
{
    [JsonPropertyName("login")] public string Login { get; init; } = string.Empty;
}