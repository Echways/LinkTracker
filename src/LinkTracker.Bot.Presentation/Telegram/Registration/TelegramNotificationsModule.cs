using LinkTracker.Bot.Application.Updates.Abstractions;
using LinkTracker.Bot.Presentation.Telegram.Notifications;
using Microsoft.Extensions.DependencyInjection;

namespace LinkTracker.Bot.Presentation.Telegram.Registration;

public static class TelegramNotificationsModule
{
    public static IServiceCollection AddTelegramNotifications(this IServiceCollection services)
    {
        services.AddSingleton<ILinkUpdateNotifier, LinkUpdateNotifier>();
        return services;
    }
}