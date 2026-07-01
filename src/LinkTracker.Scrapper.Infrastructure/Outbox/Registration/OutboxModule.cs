using LinkTracker.Scrapper.Application.Clients.Bot;
using LinkTracker.Scrapper.Infrastructure.Clients.Bot;
using LinkTracker.Scrapper.Infrastructure.Outbox.Abstractions;
using LinkTracker.Scrapper.Infrastructure.Outbox.Configuration;
using LinkTracker.Scrapper.Infrastructure.Outbox.Serialization;
using LinkTracker.Shared.Contracts.Bot;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LinkTracker.Scrapper.Infrastructure.Outbox.Registration;

public static class OutboxModule
{
    public static IServiceCollection AddOutbox(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var section = configuration.GetSection("Outbox");

        services
            .AddOptions<OutboxOptions>()
            .Bind(section)
            .Validate(o => o.DispatchIntervalSeconds > 0, "Outbox:DispatchIntervalSeconds must be greater than zero.")
            .Validate(o => o.BatchSize > 0, "Outbox:BatchSize must be greater than zero.")
            .Validate(o => o.MaxRetryCount > 0, "Outbox:MaxRetryCount must be greater than zero.")
            .ValidateOnStart();

        services.AddSingleton<IOutboxMessageSerializer, SystemTextJsonOutboxMessageSerializer>();
        services.AddSingleton<IOutboxStore, PostgresOutboxStore>();

        services.AddTransient<IBotClient>(sp =>
        {
            var outboxOptions = sp.GetRequiredService<IOptions<OutboxOptions>>().Value;

            if (outboxOptions.Enabled)
            {
                return new OutboxBotClientAdapter(sp.GetRequiredService<IOutboxStore>());
            }

            return sp.GetRequiredService<FallbackBotClient>();
        });

        return services;
    }

    private sealed class OutboxBotClientAdapter(IOutboxStore outboxStore) : IBotClient
    {
        public Task SendUpdateAsync(LinkUpdate update, CancellationToken ct = default)
        {
            return outboxStore.AddAsync(update, ct);
        }
    }
}