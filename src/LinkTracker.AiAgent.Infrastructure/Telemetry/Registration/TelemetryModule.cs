using LinkTracker.AiAgent.Application.Telemetry.Abstractions;
using LinkTracker.Shared.Infrastructure.Telemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LinkTracker.AiAgent.Infrastructure.Telemetry.Registration;

public static class TelemetryModule
{
    public static IServiceCollection AddTelemetry(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IAiAgentMetrics, AiAgentMetrics>();

        services.AddOpenTelemetryMetrics(
            "aiagent",
            AiAgentMetrics.MeterName);

        return services;
    }
}
