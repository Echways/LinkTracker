using System.Diagnostics;
using System.Net.Http.Json;
using LinkTracker.Scrapper.Application.Clients.Reddit;
using LinkTracker.Scrapper.Application.Clients.Reddit.Contracts;
using LinkTracker.Scrapper.Infrastructure.Telemetry;

namespace LinkTracker.Scrapper.Infrastructure.Clients.Reddit;

public sealed class RedditHttpClient(HttpClient httpClient, ScrapperMetrics metrics) : IRedditClient
{
    private const string Scope = "external_source";
    private const string ScopeType = "reddit.com";
    private const int PageSize = 100;

    public async Task<IReadOnlyList<RedditPostResponse>> GetNewPostsAsync(
        string subreddit,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            using var response = await httpClient.GetAsync($"/r/{subreddit}/new?limit={PageSize}", ct);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadFromJsonAsync<RedditListingEnvelope<RedditPostResponse>>(ct);

            return body is null
                ? throw new InvalidOperationException("Reddit returned an empty response body.")
                : body.Data.Children.Select(x => x.Data).ToArray();
        }
        catch
        {
            metrics.Errors.Add(
                1,
                new KeyValuePair<string, object?>("scope", Scope),
                new KeyValuePair<string, object?>("scope_type", ScopeType),
                new KeyValuePair<string, object?>("reason", "exception"));
            throw;
        }
        finally
        {
            metrics.RequestDuration.Record(
                sw.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("scope", Scope),
                new KeyValuePair<string, object?>("scope_type", ScopeType));
        }
    }
}
