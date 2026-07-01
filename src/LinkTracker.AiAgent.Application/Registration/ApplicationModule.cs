using LinkTracker.AiAgent.Application.Abstractions;
using LinkTracker.AiAgent.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LinkTracker.AiAgent.Application.Registration;

public static class ApplicationModule
{
    public static IServiceCollection AddAiAgentApplication(this IServiceCollection services)
    {
        services.AddSingleton<ILinkUpdateProcessingService, LinkUpdateProcessingService>();
        return services;
    }
}