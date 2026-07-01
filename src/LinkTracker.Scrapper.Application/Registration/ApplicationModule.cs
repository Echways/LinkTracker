using LinkTracker.Scrapper.Application.Abstractions.Tracking;
using LinkTracker.Scrapper.Application.Abstractions.Updates;
using LinkTracker.Scrapper.Application.Services.Tracking;
using LinkTracker.Scrapper.Application.Services.Updates.Clients;
using Microsoft.Extensions.DependencyInjection;

namespace LinkTracker.Scrapper.Application.Registration;

public static class ApplicationModule
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ILinkTrackingService, LinkTrackingService>();

        services.AddSingleton<ILinkUpdateHandler, GitHubLinkUpdateHandler>();
        services.AddSingleton<ILinkUpdateHandler, StackOverflowLinkUpdateHandler>();

        return services;
    }
}