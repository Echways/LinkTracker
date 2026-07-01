namespace LinkTracker.Scrapper.Infrastructure.Configuration.Clients;

public sealed class GitHubOptions
{
    public string BaseUrl { get; init; } = "https://api.github.com";

    public string? Token { get; init; }
}