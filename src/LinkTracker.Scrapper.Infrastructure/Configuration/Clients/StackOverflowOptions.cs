namespace LinkTracker.Scrapper.Infrastructure.Configuration.Clients;

public sealed class StackOverflowOptions
{
    public string BaseUrl { get; init; } = "https://api.stackexchange.com";

    public ExternalApiRateLimitOptions RateLimit { get; init; } = new()
    {
        TokenLimit = 30,
        TokensPerPeriod = 12,
        ReplenishmentPeriodSeconds = 3600
    };
}
