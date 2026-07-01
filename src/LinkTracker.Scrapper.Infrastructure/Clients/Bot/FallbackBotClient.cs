using System.Net;
using LinkTracker.Scrapper.Infrastructure.Configuration.Bot;
using LinkTracker.Shared.Contracts.Bot;
using LinkTracker.Shared.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LinkTracker.Scrapper.Infrastructure.Clients.Bot;

internal sealed class FallbackBotClient : IBotDirectClient
{
    private readonly IReadOnlyDictionary<TransportKind, IBotTransportClient> _clients;
    private readonly ILogger<FallbackBotClient> _logger;
    private readonly IOptions<BotOptions> _options;

    public FallbackBotClient(
        IEnumerable<IBotTransportClient> clients,
        IOptions<BotOptions> options,
        ILogger<FallbackBotClient> logger)
    {
        _clients = clients.ToDictionary(client => client.Transport);
        _options = options;
        _logger = logger;
    }

    public async Task SendUpdateAsync(LinkUpdate update, CancellationToken ct = default)
    {
        var transport = _options.Value.Transport;

        if (transport == TransportKind.Http)
        {
            await SendHttpWithKafkaFallbackAsync(update, ct);
            return;
        }

        await GetClient(transport).SendUpdateAsync(update, ct);
    }

    private async Task SendHttpWithKafkaFallbackAsync(LinkUpdate update, CancellationToken ct)
    {
        try
        {
            await GetClient(TransportKind.Http).SendUpdateAsync(update, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (BotClientException ex) when (ShouldFallbackToKafka(ex.StatusCode))
        {
            _logger.LogWarning(
                ex,
                "HTTP transport to Bot is unavailable. Falling back to Kafka. UpdateId={UpdateId}, StatusCode={StatusCode}",
                update.Id,
                (int)ex.StatusCode);

            await GetClient(TransportKind.Kafka).SendUpdateAsync(update, ct);
        }
    }

    private IBotTransportClient GetClient(TransportKind transport)
    {
        if (_clients.TryGetValue(transport, out var client))
        {
            return client;
        }

        throw new InvalidOperationException($"Unsupported bot transport: {transport}");
    }

    private static bool ShouldFallbackToKafka(HttpStatusCode statusCode)
    {
        return statusCode is HttpStatusCode.RequestTimeout
                   or HttpStatusCode.TooManyRequests
                   or HttpStatusCode.BadGateway
                   or HttpStatusCode.ServiceUnavailable
                   or HttpStatusCode.GatewayTimeout
               || (int)statusCode >= 500;
    }
}