namespace LinkTracker.Scrapper.Infrastructure.Configuration.Clients;

public sealed class RedditOptions
{
    public string BaseUrl { get; init; } = "https://oauth.reddit.com";

    public string TokenUrl { get; init; } = "https://www.reddit.com/api/v1/access_token";

    public string ClientId { get; init; } = string.Empty;

    public string ClientSecret { get; init; } = string.Empty;

    public ExternalApiRateLimitOptions RateLimit { get; init; } = new()
    {
        TokenLimit = 100,
        TokensPerPeriod = 100,
        ReplenishmentPeriodSeconds = 60
    };
}
