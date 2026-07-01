using LinkTracker.Bot.Application.Dialogs.Abstractions;
using LinkTracker.Bot.Infrastructure.Storage.InMemory;
using Microsoft.Extensions.DependencyInjection;

namespace LinkTracker.Bot.Infrastructure.Storage.Registration;

public static class DialogStorageModule
{
    public static IServiceCollection AddDialogStorage(this IServiceCollection services)
    {
        services.AddSingleton<IDialogStateStore, InMemoryDialogStateStore>();

        return services;
    }
}