using LinkTracker.Bot.Application.Localization;
using LinkTracker.Bot.Presentation.Telegram.Configuration;
using LinkTracker.Bot.Presentation.Telegram.Hosting;
using LinkTracker.Bot.Presentation.Telegram.Menu;
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

        services.AddSingleton<TelegramReplySender>();
        services.AddSingleton<TelegramChatMenuUpdater>();
        services.AddSingleton<IChatMenuUpdater>(sp => sp.GetRequiredService<TelegramChatMenuUpdater>());
        services.AddSingleton<UpdateMapper>();
        services.AddSingleton<UpdateReceiver>();

        services.AddHostedService<TelegramCommandsHostedService>();
        services.AddHostedService<TelegramPollingHostedService>();

        return services;
    }
}