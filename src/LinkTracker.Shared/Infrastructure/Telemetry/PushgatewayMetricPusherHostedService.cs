using System.Net;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Prometheus;

namespace LinkTracker.Shared.Infrastructure.Telemetry;

public sealed class PushgatewayMetricPusherHostedService(
    IOptions<PushgatewayOptions> options,
    ILogger<PushgatewayMetricPusherHostedService> logger) : IHostedService
{
    private MetricPusher? _pusher;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var value = options.Value;

        if (!value.Enabled)
        {
            logger.LogInformation("Pushgateway push отключён (Telemetry:Pushgateway:Enabled = false).");
            return Task.CompletedTask;
        }

        var instance = Environment.GetEnvironmentVariable("HOSTNAME")
                       ?? Dns.GetHostName();

        _pusher = new MetricPusher(new MetricPusherOptions
        {
            Endpoint = value.Endpoint,
            Job = value.Job,
            Instance = instance,
            IntervalMilliseconds = value.IntervalMilliseconds,
            OnError = ex => logger.LogWarning(
                ex,
                "Не удалось отправить метрики в Pushgateway. Endpoint={Endpoint}, Job={Job}",
                value.Endpoint,
                value.Job)
        });

        _pusher.Start();

        logger.LogInformation(
            "Запущен push метрик в Pushgateway. Endpoint={Endpoint}, Job={Job}, Instance={Instance}, IntervalMs={Interval}",
            value.Endpoint,
            value.Job,
            instance,
            value.IntervalMilliseconds);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _pusher?.Stop();
        return Task.CompletedTask;
    }
}