using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using LinkTracker.Bot.Application.Clients.Scrapper;
using LinkTracker.Bot.Application.Clients.Scrapper.Contracts.Requests;
using LinkTracker.Bot.Application.Clients.Scrapper.Contracts.Responses;
using LinkTracker.Bot.Application.Telemetry.Abstractions;
using LinkTracker.Shared.Contracts.Common;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace LinkTracker.Bot.Infrastructure.Clients.Scrapper;

public sealed class ScrapperHttpClient(HttpClient httpClient, IBotMetrics metrics) : IScrapperClient
{
    private const string Scope = "scrapper_sync_api";

    public Task RegisterChatAsync(long chatId, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            return ExecuteAsync(
                token => httpClient.PostAsync($"/tg-chat/{chatId}", null, token),
                ct);
        }
        finally
        {
            metrics.ObserveScrapperCallDuration(Scope, nameof(RegisterChatAsync), sw.Elapsed.TotalMilliseconds);
        }
    }

    public Task DeleteChatAsync(long chatId, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            return ExecuteAsync(
                token => httpClient.DeleteAsync($"/tg-chat/{chatId}", token),
                ct);
        }
        finally
        {
            metrics.ObserveScrapperCallDuration(Scope, nameof(DeleteChatAsync), sw.Elapsed.TotalMilliseconds);
        }
    }

    public Task<ListLinksResponse> GetLinksAsync(long chatId, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            return ExecuteAsync(
                async token =>
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, "/links");
                    request.Headers.Add("Tg-Chat-Id", chatId.ToString());

                    using var response = await httpClient.SendAsync(request, token);
                    await EnsureSuccessStatusCodeAsync(response, token);

                    var body = await response.Content.ReadFromJsonAsync<ListLinksResponse>(token);

                    return body ?? new ListLinksResponse();
                },
                ct);
        }
        finally
        {
            metrics.ObserveScrapperCallDuration(Scope, nameof(GetLinksAsync), sw.Elapsed.TotalMilliseconds);
        }
    }

    public Task<LinkResponse> AddLinkAsync(
        long chatId,
        Uri link,
        IReadOnlyList<string> tags,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            return ExecuteAsync(
                async token =>
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, "/links");
                    request.Headers.Add("Tg-Chat-Id", chatId.ToString());
                    request.Content = JsonContent.Create(new AddLinkRequest { Link = link, Tags = tags });

                    using var response = await httpClient.SendAsync(request, token);
                    await EnsureSuccessStatusCodeAsync(response, token);

                    var body = await response.Content.ReadFromJsonAsync<LinkResponse>(token);

                    return body ?? throw new InvalidOperationException("Scrapper вернул пустое тело ответа.");
                },
                ct);
        }
        finally
        {
            metrics.ObserveScrapperCallDuration(Scope, nameof(AddLinkAsync), sw.Elapsed.TotalMilliseconds);
        }
    }

    public Task<LinkResponse> RemoveLinkAsync(long chatId, Uri link, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            return ExecuteAsync(
                async token =>
                {
                    using var request = new HttpRequestMessage(HttpMethod.Delete, "/links");
                    request.Headers.Add("Tg-Chat-Id", chatId.ToString());
                    request.Content = JsonContent.Create(new RemoveLinkRequest { Link = link });

                    using var response = await httpClient.SendAsync(request, token);
                    await EnsureSuccessStatusCodeAsync(response, token);

                    var body = await response.Content.ReadFromJsonAsync<LinkResponse>(token);

                    return body ?? throw new InvalidOperationException("Scrapper вернул пустое тело ответа.");
                },
                ct);
        }
        finally
        {
            metrics.ObserveScrapperCallDuration(Scope, nameof(RemoveLinkAsync), sw.Elapsed.TotalMilliseconds);
        }
    }

    private static async Task ExecuteAsync(
        Func<CancellationToken, Task<HttpResponseMessage>> action,
        CancellationToken ct)
    {
        try
        {
            using var response = await action(ct);
            await EnsureSuccessStatusCodeAsync(response, ct);
        }
        catch (HttpRequestException ex)
        {
            throw CreateServiceUnavailableException(ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw CreateServiceUnavailableException(ex);
        }
        catch (TimeoutRejectedException ex)
        {
            throw CreateServiceUnavailableException(ex);
        }
        catch (BrokenCircuitException ex)
        {
            throw CreateServiceUnavailableException(ex);
        }
    }

    private static async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken ct)
    {
        try
        {
            return await action(ct);
        }
        catch (HttpRequestException ex)
        {
            throw CreateServiceUnavailableException(ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw CreateServiceUnavailableException(ex);
        }
        catch (TimeoutRejectedException ex)
        {
            throw CreateServiceUnavailableException(ex);
        }
        catch (BrokenCircuitException ex)
        {
            throw CreateServiceUnavailableException(ex);
        }
    }

    private static async Task EnsureSuccessStatusCodeAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        ApiErrorResponse? error = null;

        try
        {
            error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(ct);
        }
        catch
        {
            // ignored
        }

        var message = error?.Description ?? $"Scrapper request failed with status code {(int)response.StatusCode}.";

        throw new ScrapperClientException(response.StatusCode, message, error)
        {
            FallbackCode = response.StatusCode switch
            {
                HttpStatusCode.BadGateway => ScrapperErrorCodes.ScrapperServiceUnavailable,
                HttpStatusCode.ServiceUnavailable => ScrapperErrorCodes.ScrapperServiceUnavailable,
                HttpStatusCode.GatewayTimeout => ScrapperErrorCodes.ScrapperServiceUnavailable,
                _ => null
            }
        };
    }

    private static ScrapperClientException CreateServiceUnavailableException(Exception innerException)
    {
        return new ScrapperClientException(
            HttpStatusCode.ServiceUnavailable,
            "Scrapper сейчас недоступен.",
            innerException: innerException)
        { FallbackCode = ScrapperErrorCodes.ScrapperServiceUnavailable };
    }
}