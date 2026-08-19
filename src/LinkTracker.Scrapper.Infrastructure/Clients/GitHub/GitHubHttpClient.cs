using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;
using LinkTracker.Scrapper.Application.Clients.GitHub;
using LinkTracker.Scrapper.Application.Clients.GitHub.Contracts;
using LinkTracker.Scrapper.Infrastructure.Telemetry;

namespace LinkTracker.Scrapper.Infrastructure.Clients.GitHub;

public sealed class GitHubHttpClient(HttpClient httpClient, ScrapperMetrics metrics) : IGitHubClient
{
    private const string Scope = "external_source";
    private const string ScopeType = "github.com";
    private const int PageSize = 100;

    public Task<GitHubRepositoryResponse> GetRepositoryAsync(string owner, string repository, CancellationToken ct = default)
    {
        return SendAsync<GitHubRepositoryResponse>($"/repos/{owner}/{repository}", ct);
    }

    public async Task<IReadOnlyList<GitHubIssueResponse>> GetIssuesAsync(
        string owner,
        string repository,
        DateTimeOffset? since = null,
        CancellationToken ct = default)
    {
        var requestUri =
            $"/repos/{owner}/{repository}/issues?state=all&sort=updated&direction=desc&per_page={PageSize}";

        if (since is not null)
        {
            var sinceValue = since.Value.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
            requestUri = $"{requestUri}&since={sinceValue}";
        }

        return await SendAsync<List<GitHubIssueResponse>>(requestUri, ct);
    }

    public async Task<IReadOnlyList<GitHubPullRequestResponse>> GetPullRequestsAsync(
        string owner,
        string repository,
        CancellationToken ct = default)
    {
        return await SendAsync<List<GitHubPullRequestResponse>>(
            $"/repos/{owner}/{repository}/pulls?state=all&sort=updated&direction=desc&per_page={PageSize}",
            ct);
    }

    private async Task<T> SendAsync<T>(string requestUri, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            using var response = await httpClient.GetAsync(requestUri, ct);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadFromJsonAsync<T>(ct);
            return body ?? throw new InvalidOperationException("GitHub returned an empty response body.");
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
