using Microsoft.Extensions.DependencyInjection;

namespace LinkTracker.Bot.Application.Routing.Registration;

public static class UpdateRoutingModule
{
    public static IServiceCollection AddUpdateRouting(this IServiceCollection services)
    {
        services.AddSingleton<UpdateRouter>();

        return services;
    }
}