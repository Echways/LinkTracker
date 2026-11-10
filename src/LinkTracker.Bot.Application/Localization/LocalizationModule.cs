using Microsoft.Extensions.DependencyInjection;

namespace LinkTracker.Bot.Application.Localization;

public static class LocalizationModule
{
    public static IServiceCollection AddBotLocalization(this IServiceCollection services)
    {
        services.AddLocalization();
        services.AddSingleton<ChatLanguageService>();

        return services;
    }
}
