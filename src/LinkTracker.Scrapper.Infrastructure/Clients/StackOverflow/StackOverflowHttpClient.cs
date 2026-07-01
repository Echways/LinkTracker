using System.Diagnostics;
using System.Net.Http.Json;
using LinkTracker.Scrapper.Application.Clients.StackOverflow;
using LinkTracker.Scrapper.Application.Clients.StackOverflow.Contracts;
using LinkTracker.Scrapper.Infrastructure.Telemetry;

namespace LinkTracker.Scrapper.Infrastructure.Clients.StackOverflow;

public sealed class StackOverflowHttpClient(HttpClient httpClient, ScrapperMetrics metrics) : IStackOverflowClient
{
    private const string Scope = "external_source";
    private const string ScopeType = "stackoverflow.com";

    public async Task<StackOverflowQuestionResponse?> GetQuestionAsync(long questionId, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            using var response = await httpClient.GetAsync($"/2.3/questions/{questionId}?site=stackoverflow", ct);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadFromJsonAsync<StackOverflowQuestionsEnvelope>(ct);
            return body?.Items.FirstOrDefault();
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

    public async Task<IReadOnlyList<StackOverflowAnswerResponse>> GetAnswersAsync(long questionId, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            using var response = await httpClient.GetAsync(
                $"/2.3/questions/{questionId}/answers?site=stackoverflow&sort=creation&order=desc&filter=withbody",
                ct);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadFromJsonAsync<StackOverflowAnswersEnvelope>(ct);
            return body?.Items ?? throw new InvalidOperationException("StackOverflow returned an empty response body.");
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

    public async Task<IReadOnlyList<StackOverflowCommentResponse>> GetCommentsAsync(long questionId, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            using var response = await httpClient.GetAsync(
                $"/2.3/questions/{questionId}/comments?site=stackoverflow&sort=creation&order=desc&filter=withbody",
                ct);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadFromJsonAsync<StackOverflowCommentsEnvelope>(ct);
            return body?.Items ?? throw new InvalidOperationException("StackOverflow returned an empty response body.");
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