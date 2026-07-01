using System.Net;
using System.Net.Http.Json;
using LinkTracker.Shared.Contracts.Bot;
using LinkTracker.Shared.Contracts.Common;
using LinkTracker.Shared.Infrastructure;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace LinkTracker.Scrapper.Infrastructure.Clients.Bot;

internal sealed class BotHttpClient(HttpClient httpClient) : IBotTransportClient
{
    public TransportKind Transport => TransportKind.Http;

    public async Task SendUpdateAsync(LinkUpdate update, CancellationToken ct = default)
    {
        try
        {
            using var response = await httpClient.PostAsJsonAsync("/updates", update, ct);
            await EnsureSuccessStatusCodeAsync(response, ct);
        }
        catch (BotClientException)
        {
            throw;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            throw CreateServiceUnavailableException(ex);
        }
        catch (TaskCanceledException ex)
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

        var message = error?.Description ?? $"Bot request failed with status code {(int)response.StatusCode}.";

        throw new BotClientException(response.StatusCode, message, error);
    }

    private static BotClientException CreateServiceUnavailableException(Exception innerException)
    {
        return new BotClientException(
            HttpStatusCode.ServiceUnavailable,
            "Bot сейчас недоступен по HTTP.",
            innerException: innerException);
    }
}