using LinkTracker.Bot.Application.Telemetry.Abstractions;
using LinkTracker.Shared.Infrastructure.Telemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LinkTracker.Bot.Infrastructure.Telemetry.Registration;

public static class TelemetryModule
{
    public static IServiceCollection AddTelemetry(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IBotMetrics, BotMetrics>();

        services.AddOpenTelemetryMetrics(
            "bot",
            BotMetrics.MeterName);

        return services;
    }
}