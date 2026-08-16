using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace LinkTracker.Shared.Infrastructure.Telemetry;

public static class OpenTelemetryRegistration
{
    public static IServiceCollection AddOpenTelemetryMetrics(
        this IServiceCollection services,
        string serviceName,
        params string[] meterNames)
    {
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

                metrics.AddPrometheusExporter();
            });

        return services;
    }

    public static IEndpointConventionBuilder MapMetricsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints
            .MapPrometheusScrapingEndpoint()
            .DisableRateLimiting();
    }
}
