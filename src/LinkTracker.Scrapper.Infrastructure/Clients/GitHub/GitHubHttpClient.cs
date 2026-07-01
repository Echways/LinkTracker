using System.Diagnostics;
using System.Net.Http.Json;
using LinkTracker.Scrapper.Application.Clients.GitHub;
using LinkTracker.Scrapper.Application.Clients.GitHub.Contracts;
using LinkTracker.Scrapper.Infrastructure.Telemetry;

namespace LinkTracker.Scrapper.Infrastructure.Clients.GitHub;

public sealed class GitHubHttpClient(HttpClient httpClient, ScrapperMetrics metrics) : IGitHubClient
{
    private const string Scope = "external_source";
    private const string ScopeType = "github.com";

    public async Task<GitHubRepositoryResponse> GetRepositoryAsync(string owner, string repository, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            using var response = await httpClient.GetAsync($"/repos/{owner}/{repository}", ct);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadFromJsonAsync<GitHubRepositoryResponse>(ct);
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

    public async Task<IReadOnlyList<GitHubIssueResponse>> GetIssuesAsync(
        string owner,
        string repository,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            using var response = await httpClient.GetAsync($"/repos/{owner}/{repository}/issues", ct);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadFromJsonAsync<List<GitHubIssueResponse>>(ct);
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

    public async Task<IReadOnlyList<GitHubPullRequestResponse>> GetPullRequestsAsync(
        string owner,
        string repository,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            using var response = await httpClient.GetAsync($"/repos/{owner}/{repository}/pulls", ct);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadFromJsonAsync<List<GitHubPullRequestResponse>>(ct);
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