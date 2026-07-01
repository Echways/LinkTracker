using Dapper;
using Npgsql;

namespace LinkTracker.Scrapper.Storage.Sql.Helpers;

internal static class SqlTagsHelper
{
    public static Task<long?> GetSubscriptionIdForTagUpdateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long chatId,
        string normalizedUrl,
        CancellationToken ct)
    {
        return connection.ExecuteScalarAsync<long?>(
            new CommandDefinition(
                SqlLinkTrackingStoreCommands.GetSubscriptionIdForTagUpdate,
                new { chatId, normalizedUrl },
                transaction,
                cancellationToken: ct));
    }

    public static Task<long> GetOrCreateTagIdAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string tag,
        CancellationToken ct)
    {
        return connection.ExecuteScalarAsync<long>(
            new CommandDefinition(
                SqlLinkTrackingStoreCommands.GetOrCreateTag,
                new { name = tag },
                transaction,
                cancellationToken: ct));
    }

    public static Task AttachTagAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long subscriptionId,
        long tagId,
        CancellationToken ct)
    {
        return connection.ExecuteAsync(
            new CommandDefinition(
                SqlLinkTrackingStoreCommands.AttachTag,
                new { subscriptionId, tagId },
                transaction,
                cancellationToken: ct));
    }

    public static Task<bool> TagUsageExistsForChatAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long chatId,
        string tag,
        CancellationToken ct)
    {
        return connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                SqlLinkTrackingStoreCommands.TagUsageExistsForChat,
                new { chatId, tag },
                transaction,
                cancellationToken: ct));
    }

    public static Task RenameTagLinksAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long chatId,
        string tag,
        long newTagId,
        CancellationToken ct)
    {
        return connection.ExecuteAsync(
            new CommandDefinition(
                SqlLinkTrackingStoreCommands.RenameTagLinks,
                new { chatId, tag, newTagId },
                transaction,
                cancellationToken: ct));
    }

    public static Task DeleteTagLinksAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long chatId,
        string tag,
        CancellationToken ct)
    {
        return connection.ExecuteAsync(
            new CommandDefinition(
                SqlLinkTrackingStoreCommands.DeleteTagLinks,
                new { chatId, tag },
                transaction,
                cancellationToken: ct));
    }

    public static Task DeleteOrphanTagByNameAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string tag,
        CancellationToken ct)
    {
        return connection.ExecuteAsync(
            new CommandDefinition(
                SqlLinkTrackingStoreCommands.DeleteOrphanTagByName,
                new { name = tag },
                transaction,
                cancellationToken: ct));
    }
}