using Dapper;
using LinkTracker.Scrapper.Storage.Abstractions.Models;
using LinkTracker.Scrapper.Storage.Sql.Helpers;
using LinkTracker.Scrapper.Storage.Sql.Models;
using LinkTracker.Shared.Links;
using Npgsql;

namespace LinkTracker.Scrapper.Storage.Sql;

public sealed class SqlLinkTrackingStore(NpgsqlDataSource dataSource) : ILinkTrackingStore
{
    public async Task<bool> TryRegisterChatAsync(long chatId, CancellationToken ct = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);

        var affected = await connection.ExecuteAsync(
            new CommandDefinition(
                SqlLinkTrackingStoreCommands.TryRegisterChat,
                new { chatId },
                cancellationToken: ct));

        return affected > 0;
    }

    public async Task<bool> TryDeleteChatAsync(long chatId, CancellationToken ct = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);

        var affected = await connection.ExecuteAsync(
            new CommandDefinition(
                SqlLinkTrackingStoreCommands.TryDeleteChat,
                new { chatId },
                cancellationToken: ct));

        return affected > 0;
    }

    public async Task<bool> ChatExistsAsync(long chatId, CancellationToken ct = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);

        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                SqlLinkTrackingStoreCommands.ChatExists,
                new { chatId },
                cancellationToken: ct));
    }

    public async Task<IReadOnlyList<TrackedLinkRecord>> GetAllTrackedLinkRecordsAsync(
        long chatId,
        CancellationToken ct = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);

        var rows = await connection.QueryAsync<TrackedLinkRow>(
            new CommandDefinition(
                SqlLinkTrackingStoreCommands.GetTrackedLinkRows,
                new { chatId },
                cancellationToken: ct));

        return rows
            .Select(MapTrackedLinkRecord)
            .ToArray();
    }

    public async Task<TrackedLinkRecord?> TryAddAsync(
        long chatId,
        Uri link,
        IReadOnlyList<string> tags,
        CancellationToken ct = default)
    {
        var normalizedUrl = TrackedLinkUrl.Normalize(link);
        var cleanedTags = tags
            .Where(static x => string.IsNullOrWhiteSpace(x) is false)
            .Select(static x => x.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        try
        {
            var linkId = await connection.ExecuteScalarAsync<long>(
                new CommandDefinition(
                    SqlLinkTrackingStoreCommands.GetOrCreateLink,
                    new { url = link.ToString(), normalizedUrl },
                    transaction,
                    cancellationToken: ct));

            var subscriptionId = await connection.ExecuteScalarAsync<long?>(
                new CommandDefinition(
                    SqlLinkTrackingStoreCommands.CreateSubscription,
                    new { chatId, linkId },
                    transaction,
                    cancellationToken: ct));

            if (subscriptionId is null)
            {
                await transaction.RollbackAsync(ct);
                return null;
            }

            foreach (var tag in cleanedTags)
            {
                var tagId = await connection.ExecuteScalarAsync<long>(
                    new CommandDefinition(
                        SqlLinkTrackingStoreCommands.GetOrCreateTag,
                        new { name = tag },
                        transaction,
                        cancellationToken: ct));

                await connection.ExecuteAsync(
                    new CommandDefinition(
                        SqlLinkTrackingStoreCommands.AttachTag,
                        new { subscriptionId = subscriptionId.Value, tagId },
                        transaction,
                        cancellationToken: ct));
            }

            await transaction.CommitAsync(ct);

            return new TrackedLinkRecord
            {
                Id = linkId,
                Url = link,
                Tags = cleanedTags,
                LastUpdatedAt = null,
                LastEventKey = null
            };
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<TrackedLinkRecord?> TryRemoveAsync(long chatId, Uri link, CancellationToken ct = default)
    {
        var normalizedUrl = TrackedLinkUrl.Normalize(link);

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        try
        {
            var row = await connection.QuerySingleOrDefaultAsync<SubscriptionRemovalRow>(
                new CommandDefinition(
                    SqlLinkTrackingStoreCommands.GetTrackedLinkForSubscriptionRemoval,
                    new { chatId, normalizedUrl },
                    transaction,
                    cancellationToken: ct));

            if (row is null)
            {
                await transaction.RollbackAsync(ct);
                return null;
            }

            await connection.ExecuteAsync(
                new CommandDefinition(
                    SqlLinkTrackingStoreCommands.DeleteSubscription,
                    new { subscriptionId = row.SubscriptionId },
                    transaction,
                    cancellationToken: ct));

            foreach (var tag in row.Tags.Distinct(StringComparer.Ordinal))
            {
                await SqlTagsHelper.DeleteOrphanTagByNameAsync(connection, transaction, tag, ct);
            }

            var hasSubscriptions = await connection.ExecuteScalarAsync<bool>(
                new CommandDefinition(
                    SqlLinkTrackingStoreCommands.LinkHasSubscriptions,
                    new { linkId = row.LinkId },
                    transaction,
                    cancellationToken: ct));

            if (!hasSubscriptions)
            {
                await connection.ExecuteAsync(
                    new CommandDefinition(
                        SqlLinkTrackingStoreCommands.DeleteLink,
                        new { linkId = row.LinkId },
                        transaction,
                        cancellationToken: ct));
            }

            await transaction.CommitAsync(ct);

            return new TrackedLinkRecord
            {
                Id = row.LinkId,
                Url = new Uri(row.Url),
                LastUpdatedAt = row.LastUpdatedAt,
                LastEventKey = row.LastEventKey,
                Tags = row.Tags,
            };
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<bool> TryCreateTagAsync(string tag, CancellationToken ct = default)
    {
        var normalizedTag = tag.Trim();

        await using var connection = await dataSource.OpenConnectionAsync(ct);

        var createdId = await connection.ExecuteScalarAsync<long?>(
            new CommandDefinition(
                SqlLinkTrackingStoreCommands.TryCreateTag,
                new { name = normalizedTag },
                cancellationToken: ct));

        return createdId is not null;
    }

    public async Task<IReadOnlyList<string>> GetTagsAsync(long chatId, CancellationToken ct = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);

        var rows = await connection.QueryAsync<TagRow>(
            new CommandDefinition(
                SqlLinkTrackingStoreCommands.GetTagRows,
                new { chatId },
                cancellationToken: ct));

        return rows.Select(x => x.Name).ToArray();
    }

    public async Task<TrackedLinkRecord?> TryAddTagAsync(long chatId, Uri link, string tag, CancellationToken ct = default)
    {
        var normalizedUrl = TrackedLinkUrl.Normalize(link);

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        try
        {
            var subscriptionId = await SqlTagsHelper.GetSubscriptionIdForTagUpdateAsync(
                connection,
                transaction,
                chatId,
                normalizedUrl,
                ct);

            if (subscriptionId is null)
            {
                await transaction.RollbackAsync(ct);
                return null;
            }

            var tagId = await SqlTagsHelper.GetOrCreateTagIdAsync(connection, transaction, tag, ct);

            await SqlTagsHelper.AttachTagAsync(
                connection,
                transaction,
                subscriptionId.Value,
                tagId,
                ct);

            var updatedRecord = await GetTrackedLinkForTagUpdateAsync(
                connection,
                transaction,
                chatId,
                normalizedUrl,
                ct);

            await transaction.CommitAsync(ct);
            return updatedRecord;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<bool> TryRenameTagAsync(long chatId, string tag, string newTag, CancellationToken ct = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        try
        {
            var hasTag = await SqlTagsHelper.TagUsageExistsForChatAsync(
                connection,
                transaction,
                chatId,
                tag,
                ct);

            if (!hasTag)
            {
                await transaction.RollbackAsync(ct);
                return false;
            }

            var newTagId = await SqlTagsHelper.GetOrCreateTagIdAsync(
                connection,
                transaction,
                newTag,
                ct);

            await SqlTagsHelper.RenameTagLinksAsync(
                connection,
                transaction,
                chatId,
                tag,
                newTagId,
                ct);

            await SqlTagsHelper.DeleteTagLinksAsync(
                connection,
                transaction,
                chatId,
                tag,
                ct);

            await SqlTagsHelper.DeleteOrphanTagByNameAsync(
                connection,
                transaction,
                tag,
                ct);

            await transaction.CommitAsync(ct);
            return true;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<bool> TryDeleteTagAsync(long chatId, string tag, CancellationToken ct = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        try
        {
            var hasTag = await SqlTagsHelper.TagUsageExistsForChatAsync(
                connection,
                transaction,
                chatId,
                tag,
                ct);

            if (!hasTag)
            {
                await transaction.RollbackAsync(ct);
                return false;
            }

            await SqlTagsHelper.DeleteTagLinksAsync(
                connection,
                transaction,
                chatId,
                tag,
                ct);

            await SqlTagsHelper.DeleteOrphanTagByNameAsync(
                connection,
                transaction,
                tag,
                ct);

            await transaction.CommitAsync(ct);
            return true;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<IReadOnlyList<TrackedLinkSubscription>> GetSubscriptionsDueForCheckAsync(
        DateTimeOffset checkedBefore,
        int batchSize,
        CancellationToken ct = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);

        var rows = await connection.QueryAsync<SubscriptionRow>(
            new CommandDefinition(
                SqlLinkTrackingStoreCommands.GetSubscriptionsDueForCheckRows,
                new { checkedBefore, batchSize },
                cancellationToken: ct));

        return MapSubscriptions(rows);
    }

    public async Task MarkCheckedAsync(
        IReadOnlyCollection<long> linkIds,
        DateTimeOffset checkedAt,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(linkIds);

        if (linkIds.Count == 0)
        {
            return;
        }

        await using var connection = await dataSource.OpenConnectionAsync(ct);

        await connection.ExecuteAsync(
            new CommandDefinition(
                SqlLinkTrackingStoreCommands.MarkChecked,
                new { linkIds = linkIds.ToArray(), checkedAt },
                cancellationToken: ct));
    }

    public async Task SetCursorAsync(long linkId, DateTimeOffset lastUpdatedAt, string? lastEventKey, CancellationToken ct = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);

        await connection.ExecuteAsync(
            new CommandDefinition(
                SqlLinkTrackingStoreCommands.SetCursor,
                new { linkId, lastUpdatedAt, lastEventKey },
                cancellationToken: ct));
    }

    private static async Task<TrackedLinkRecord?> GetTrackedLinkForTagUpdateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long chatId,
        string normalizedUrl,
        CancellationToken ct)
    {
        var row = await connection.QuerySingleOrDefaultAsync<SubscriptionRemovalRow>(
            new CommandDefinition(
                SqlLinkTrackingStoreCommands.GetTrackedLinkForTagUpdate,
                new { chatId, normalizedUrl },
                transaction,
                cancellationToken: ct));

        if (row is null)
        {
            return null;
        }

        return new TrackedLinkRecord
        {
            Id = row.LinkId,
            Url = new Uri(row.Url),
            LastUpdatedAt = row.LastUpdatedAt,
            LastEventKey = row.LastEventKey,
            Tags = row.Tags,
        };
    }

    private static TrackedLinkRecord MapTrackedLinkRecord(TrackedLinkRow row)
    {
        return new TrackedLinkRecord
        {
            Id = row.Id,
            Url = new Uri(row.Url),
            LastUpdatedAt = row.LastUpdatedAt,
            LastEventKey = row.LastEventKey,
            Tags = row.Tags,
        };
    }

    private static IReadOnlyList<TrackedLinkSubscription> MapSubscriptions(
        IEnumerable<SubscriptionRow> rows)
    {
        return rows
            .GroupBy(x => x.Id)
            .Select(group =>
            {
                var first = group.First();

                return new TrackedLinkSubscription
                {
                    Id = first.Id,
                    Url = new Uri(first.Url),
                    LastUpdatedAt = first.LastUpdatedAt,
                    LastEventKey = first.LastEventKey,
                    TgChatIds = group.Select(x => x.ChatId).Distinct().ToArray()
                };
            })
            .ToArray();
    }
}