using LinkTracker.Bot.Application.Callbacks;
using LinkTracker.Bot.Application.Clients.Scrapper;
using LinkTracker.Bot.Application.Commands;
using LinkTracker.Bot.Application.Commands.Registration;
using LinkTracker.Bot.Application.Dialogs.Registration;
using LinkTracker.Bot.Application.Localization;
using LinkTracker.Bot.Application.Routing;
using LinkTracker.Bot.Application.Routing.Registration;
using LinkTracker.Bot.Application.Telemetry.Abstractions;
using LinkTracker.Bot.Infrastructure.Configuration.Valkey;
using LinkTracker.Bot.Infrastructure.Storage.InMemory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using LinkTracker.Bot.Application.Dialogs.Abstractions;

namespace LinkTracker.Tests.Bot.Unit.Application;

[Trait("Module", "Bot")]
[Trait("Category", "Unit")]
public sealed class BotCompositionTests
{
    [Fact]
    public void Container_ResolvesRouterCommandsAndCallbacks()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBotLocalization();
        services.AddCommands();
        services.AddCallbacks();
        services.AddDialogs();
        services.AddUpdateRouting();
        services.AddSingleton(Substitute.For<IScrapperClient>());
        services.AddSingleton(Substitute.For<IBotMetrics>());
        services.AddSingleton(Substitute.For<IChatMenuUpdater>());
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(Options.Create(new DialogStateStoreOptions()));
        services.AddSingleton<IDialogStateStore, InMemoryDialogStateStore>();
        services.AddSingleton<ILanguageStore, InMemoryLanguageStore>();

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        Assert.NotNull(provider.GetRequiredService<UpdateRouter>());
        Assert.Contains(provider.GetServices<ICommandDescriptor>(), d => d.Name == "language");
        Assert.NotEmpty(provider.GetServices<ICallbackHandler>());
        Assert.All(
            provider.GetRequiredService<CommandRegistry>().GetTelegramCommands(),
            c => Assert.False(string.IsNullOrWhiteSpace(c.Description)));
    }
}
