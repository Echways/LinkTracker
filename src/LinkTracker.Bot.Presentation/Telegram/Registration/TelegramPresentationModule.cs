using LinkTracker.Bot.Application.Updates.Abstractions;
using LinkTracker.Bot.Presentation.Telegram.Configuration;
using LinkTracker.Bot.Presentation.Telegram.Hosting;
using LinkTracker.Bot.Presentation.Telegram.Notifications;
using LinkTracker.Bot.Presentation.Telegram.Updates;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Telegram.Bot;

namespace LinkTracker.Bot.Presentation.Telegram.Registration;

public static class TelegramPresentationModule
{
    public static IServiceCollection AddTelegramPresentation(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<BotOptions>()
            .Bind(configuration.GetSection("Bot"))
            .Validate(o => !string.IsNullOrWhiteSpace(o.Token), "Bot:Token must be set")
            .ValidateOnStart();

        services.AddSingleton<ITelegramBotClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<BotOptions>>().Value;
            return new TelegramBotClient(options.Token);
        });

        services.AddSingleton<UpdateMapper>();
        services.AddSingleton<UpdateReceiver>();

        services.AddSingleton<ILinkUpdateNotifier, LinkUpdateNotifier>();

        services.AddHostedService<TelegramCommandsHostedService>();
        services.AddHostedService<TelegramPollingHostedService>();

        return services;
    }
}