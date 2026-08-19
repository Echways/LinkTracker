using LinkTracker.Bot.Application.Dialogs.Abstractions;
using LinkTracker.Bot.Infrastructure.Configuration.Valkey;
using LinkTracker.Bot.Infrastructure.Storage.InMemory;
using LinkTracker.Bot.Infrastructure.Storage.Valkey;
using LinkTracker.Shared.Infrastructure.Valkey;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace LinkTracker.Bot.Infrastructure.Storage.Registration;

public static class DialogStorageModule
{
    public static IServiceCollection AddDialogStorage(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<DialogStateStoreOptions>()
            .Bind(configuration.GetSection(DialogStateStoreOptions.SectionName))
            .Validate(
                o => o.DialogTtlSeconds > 0,
                "Valkey:DialogTtlSeconds must be positive.")
            .Validate(
                o => !o.Enabled || !string.IsNullOrWhiteSpace(o.ConnectionString),
                "Valkey connection string is required when the Valkey dialog state store is enabled.")
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);

        var options = configuration
            .GetSection(DialogStateStoreOptions.SectionName)
            .Get<DialogStateStoreOptions>() ?? new DialogStateStoreOptions();

        if (!options.Enabled)
        {
            services.AddSingleton<IDialogStateStore, InMemoryDialogStateStore>();

            return services;
        }

        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var storeOptions = sp.GetRequiredService<IOptions<DialogStateStoreOptions>>().Value;

            return ConnectionMultiplexer.Connect(ValkeyConfiguration.Parse(storeOptions.ConnectionString));
        });

        services.AddSingleton<IDialogStateStore, ValkeyDialogStateStore>();

        return services;
    }
}
