namespace LinkTracker.Scrapper.Infrastructure.Clients.RateLimiting;

public sealed class ExternalApiRateLimitedException(string apiName)
    : Exception($"Rate limit for external API '{apiName}' is exhausted; the request was not sent.")
{
    public string ApiName { get; } = apiName;
}
