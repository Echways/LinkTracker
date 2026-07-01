using LinkTracker.Bot.Application.Dialogs.Abstractions;
using LinkTracker.Bot.Application.Dialogs.Implementations.Track;
using LinkTracker.Bot.Application.Dialogs.Implementations.Track.Nodes;
using LinkTracker.Bot.Application.Dialogs.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace LinkTracker.Bot.Application.Dialogs.Registration;

public static class DialogsModule
{
    public static IServiceCollection AddDialogs(this IServiceCollection services)
    {
        services.AddSingleton<DialogManager>();

        services.AddSingleton<AskUrlNode>();
        services.AddSingleton<AskTagsNode>();
        services.AddSingleton<TrackConfirmNode>();
        services.AddSingleton<IDialog, TrackDialog>();

        return services;
    }
}