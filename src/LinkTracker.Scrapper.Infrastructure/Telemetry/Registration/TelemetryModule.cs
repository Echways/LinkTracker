using LinkTracker.Shared.Infrastructure.Telemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LinkTracker.Scrapper.Infrastructure.Telemetry.Registration;

public static class TelemetryModule
{
    public static IServiceCollection AddTelemetry(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<ScrapperMetrics>();

        services.AddOpenTelemetryMetricsWithPushgateway(
            configuration,
            "scrapper",
            "scrapper",
            ScrapperMetrics.MeterName,
            "Npgsql");

        return services;
    }
}