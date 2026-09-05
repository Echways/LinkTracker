namespace LinkTracker.Scrapper.Infrastructure.Clients.Reddit;

internal interface IRedditAccessTokenProvider
{
    Task<string> GetAccessTokenAsync(CancellationToken ct = default);
}
