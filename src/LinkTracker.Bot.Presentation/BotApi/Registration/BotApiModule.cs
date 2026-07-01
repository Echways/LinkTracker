using Microsoft.Extensions.DependencyInjection;

namespace LinkTracker.Bot.Presentation.BotApi.Registration;

public static class BotApiModule
{
    public static IServiceCollection AddBotApi(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        return services;
    }
}