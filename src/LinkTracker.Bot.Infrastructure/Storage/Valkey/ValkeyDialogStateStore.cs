using System.Text.Json;
using LinkTracker.Bot.Application.Dialogs.Abstractions;
using LinkTracker.Bot.Infrastructure.Configuration.Valkey;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace LinkTracker.Bot.Infrastructure.Storage.Valkey;

internal sealed class ValkeyDialogStateStore(
    IConnectionMultiplexer connection,
    IOptions<DialogStateStoreOptions> options,
    ILogger<ValkeyDialogStateStore> logger) : IDialogStateStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly DialogStateStoreOptions _options = options.Value;

    public async Task<DialogContext> GetOrCreateAsync(long chatId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var key = BuildKey(chatId);

        try
        {
            var value = await connection.GetDatabase().StringGetAsync(key);

            if (value.IsNullOrEmpty)
            {
                return new DialogContext { ChatId = chatId };
            }

            var context = JsonSerializer.Deserialize<DialogContext>((string)value!, SerializerOptions);

            return context ?? new DialogContext { ChatId = chatId };
        }
        catch (Exception ex) when (ex is RedisException or JsonException)
        {
            logger.LogError(
                ex,
                "Failed to read dialog state, the dialog will restart. ChatId={ChatId}, Key={Key}",
                chatId,
                key);

            return new DialogContext { ChatId = chatId };
        }
    }

    public async Task SaveAsync(DialogContext ctx, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(ctx);

        var key = BuildKey(ctx.ChatId);

        try
        {
            await connection.GetDatabase().StringSetAsync(
                key,
                JsonSerializer.Serialize(ctx, SerializerOptions),
                TimeSpan.FromSeconds(_options.DialogTtlSeconds));
        }
        catch (RedisException ex)
        {
            logger.LogError(
                ex,
                "Failed to save dialog state. ChatId={ChatId}, Key={Key}",
                ctx.ChatId,
                key);
        }
    }

    public async Task ResetAsync(long chatId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var key = BuildKey(chatId);

        try
        {
            await connection.GetDatabase().KeyDeleteAsync(key);
        }
        catch (RedisException ex)
        {
            logger.LogError(
                ex,
                "Failed to reset dialog state. ChatId={ChatId}, Key={Key}",
                chatId,
                key);
        }
    }

    private string BuildKey(long chatId)
    {
        return $"{_options.InstanceName}:dialog:{{chat:{chatId}}}";
    }
}
