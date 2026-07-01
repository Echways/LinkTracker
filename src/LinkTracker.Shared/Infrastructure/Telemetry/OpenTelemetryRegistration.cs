using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace LinkTracker.Shared.Infrastructure.Telemetry;

public static class OpenTelemetryRegistration
{
    public static IServiceCollection AddOpenTelemetryMetricsWithPushgateway(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName,
        string job,
        params string[] meterNames)
    {
        services
            .AddOptions<PushgatewayOptions>()
            .Bind(configuration.GetSection(PushgatewayOptions.SectionName))
            .PostConfigure(opt =>
            {
                if (string.IsNullOrWhiteSpace(opt.Job) || opt.Job == "app")
                {
                    opt.Job = job;
                }
            });

        services
            .AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();

                foreach (var meterName in meterNames)
                {
                    metrics.AddMeter(meterName);
                }
            });

        services.AddHostedService<PushgatewayMetricPusherHostedService>();

        return services;
    }
}