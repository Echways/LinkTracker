using LinkTracker.Bot.Application.Commands.Implementations;
using Microsoft.Extensions.DependencyInjection;

namespace LinkTracker.Bot.Application.Commands.Registration;

public static class CommandsModule
{
    public static IServiceCollection AddCommands(this IServiceCollection services)
    {
        services.AddSingleton(sp =>
            new Lazy<IEnumerable<ICommandDescriptor>>(sp.GetServices<ICommandDescriptor>, true));

        Register<StartCommand>(services);
        Register<HelpCommand>(services);
        Register<CancelCommand>(services);
        Register<TrackCommand>(services);
        Register<UntrackCommand>(services);
        Register<ListCommand>(services);

        services.AddSingleton<CommandRouter>();
        services.AddSingleton<CommandRegistry>();

        return services;
    }

    private static void Register<T>(IServiceCollection services)
        where T : class, ICommandHandler, ICommandDescriptor
    {
        services.AddSingleton<T>();
        services.AddSingleton<ICommandHandler>(sp => sp.GetRequiredService<T>());
        services.AddSingleton<ICommandDescriptor>(sp => sp.GetRequiredService<T>());
    }
}