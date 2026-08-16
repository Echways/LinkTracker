using LinkTracker.Scrapper.Storage.Abstractions.Models;
using LinkTracker.Scrapper.Storage.Orm.Entities;
using LinkTracker.Scrapper.Storage.Orm.Helpers;
using LinkTracker.Shared.Links;
using Microsoft.EntityFrameworkCore;

namespace LinkTracker.Scrapper.Storage.Orm;

public sealed class OrmLinkTrackingStore(IDbContextFactory<AppDbContext> dbContextFactory) : ILinkTrackingStore
{
    public async Task<bool> TryRegisterChatAsync(long chatId, CancellationToken ct = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(ct);

        var exists = await dbContext.Chats
            .AnyAsync(x => x.Id == chatId, ct);

        if (exists)
        {
            return false;
        }

        dbContext.Chats.Add(new ChatEntity { Id = chatId });

        await dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> TryDeleteChatAsync(long chatId, CancellationToken ct = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(ct);

        var chat = await dbContext.Chats
            .FirstOrDefaultAsync(x => x.Id == chatId, ct);

        if (chat is null)
        {
            return false;
        }

        dbContext.Chats.Remove(chat);
        await dbContext.SaveChangesAsync(ct);

        return true;
    }

    public async Task<bool> ChatExistsAsync(long chatId, CancellationToken ct = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(ct);

        return await dbContext.Chats.AnyAsync(x => x.Id == chatId, ct);
    }

    public async Task<IReadOnlyList<TrackedLinkRecord>> GetAllTrackedLinkRecordsAsync(long chatId, CancellationToken ct = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(ct);

        return await dbContext.Subscriptions
            .AsNoTracking()
            .Where(x => x.ChatId == chatId)
            .OrderBy(x => x.LinkId)
            .Select(x => new TrackedLinkRecord
            {
                Id = x.LinkId,
                Url = new Uri(x.Link.Url),
                LastUpdatedAt = x.Link.LastUpdatedAt,
                LastEventKey = x.Link.LastEventKey,
                Tags = x.SubscriptionTags
                    .Select(t => t.Tag.Name)
                    .Distinct()
                    .ToArray(),
            })
            .ToArrayAsync(ct);
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

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(ct);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);

        try
        {
            var linkEntity = await dbContext.Links
                .FirstOrDefaultAsync(x => x.NormalizedUrl == normalizedUrl, ct);

            if (linkEntity is null)
            {
                linkEntity = new LinkEntity { Url = link.ToString(), NormalizedUrl = normalizedUrl, LastUpdatedAt = null, LastEventKey = null };

                dbContext.Links.Add(linkEntity);
                await dbContext.SaveChangesAsync(ct);
            }

            var subscriptionExists = await dbContext.Subscriptions
                .AnyAsync(x => x.ChatId == chatId && x.LinkId == linkEntity.Id, ct);

            if (subscriptionExists)
            {
                return null;
            }

            var subscription = new SubscriptionEntity { ChatId = chatId, LinkId = linkEntity.Id };

            dbContext.Subscriptions.Add(subscription);
            await dbContext.SaveChangesAsync(ct);

            if (cleanedTags.Length > 0)
            {
                var tagsByName = await OrmTagsHelper.GetOrCreateTagsAsync(dbContext, cleanedTags, ct);

                foreach (var tagName in cleanedTags)
                {
                    dbContext.SubscriptionTags.Add(new SubscriptionTagEntity { SubscriptionId = subscription.Id, TagId = tagsByName[tagName].Id });
                }

                await dbContext.SaveChangesAsync(ct);
            }

            await transaction.CommitAsync(ct);

            return new TrackedLinkRecord
            {
                Id = linkEntity.Id,
                Url = link,
                Tags = cleanedTags,
                LastUpdatedAt = linkEntity.LastUpdatedAt,
                LastEventKey = linkEntity.LastEventKey
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

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(ct);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);

        try
        {
            var subscription = await dbContext.Subscriptions
                .Include(x => x.Link)
                .Include(x => x.SubscriptionTags)
                .ThenInclude(x => x.Tag)
                .FirstOrDefaultAsync(
                    x => x.ChatId == chatId && x.Link.NormalizedUrl == normalizedUrl,
                    ct);

            if (subscription is null)
            {
                return null;
            }

            var result = new TrackedLinkRecord
            {
                Id = subscription.LinkId,
                Url = new Uri(subscription.Link.Url),
                LastUpdatedAt = subscription.Link.LastUpdatedAt,
                Tags = subscription.SubscriptionTags
                    .Select(x => x.Tag.Name)
                    .Distinct()
                    .ToArray(),
            };

            var linkId = subscription.LinkId;

            var tagNames = subscription.SubscriptionTags
                .Select(x => x.Tag.Name)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            dbContext.Subscriptions.Remove(subscription);
            await dbContext.SaveChangesAsync(ct);

            foreach (var tagName in tagNames)
            {
                await OrmTagsHelper.DeleteOrphanTagByNameAsync(dbContext, tagName, ct);
            }

            await dbContext.SaveChangesAsync(ct);

            var linkHasSubscriptions = await dbContext.Subscriptions
                .AnyAsync(x => x.LinkId == linkId, ct);

            if (!linkHasSubscriptions)
            {
                var linkEntity = await dbContext.Links
                    .FirstOrDefaultAsync(x => x.Id == linkId, ct);

                if (linkEntity is not null)
                {
                    dbContext.Links.Remove(linkEntity);
                    await dbContext.SaveChangesAsync(ct);
                }
            }

            await transaction.CommitAsync(ct);
            return result;
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

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(ct);

        var exists = await dbContext.Tags
            .AnyAsync(x => x.Name == normalizedTag, ct);

        if (exists)
        {
            return false;
        }

        dbContext.Tags.Add(new TagEntity { Name = normalizedTag });

        await dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<string>> GetTagsAsync(long chatId, CancellationToken ct = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(ct);

        return await dbContext.Subscriptions
            .AsNoTracking()
            .Where(x => x.ChatId == chatId)
            .SelectMany(x => x.SubscriptionTags.Select(st => st.Tag.Name))
            .Distinct()
            .OrderBy(x => x)
            .ToArrayAsync(ct);
    }

    public async Task<TrackedLinkRecord?> TryAddTagAsync(long chatId, Uri link, string tag, CancellationToken ct = default)
    {
        var normalizedUrl = TrackedLinkUrl.Normalize(link);
        var cleanedTag = tag.Trim();

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(ct);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);

        try
        {
            var subscription = await dbContext.Subscriptions
                .Include(x => x.Link)
                .Include(x => x.SubscriptionTags)
                .ThenInclude(x => x.Tag)
                .FirstOrDefaultAsync(
                    x => x.ChatId == chatId && x.Link.NormalizedUrl == normalizedUrl,
                    ct);

            if (subscription is null)
            {
                await transaction.RollbackAsync(ct);
                return null;
            }

            var tagsByName = await OrmTagsHelper.GetOrCreateTagsAsync(dbContext, [cleanedTag], ct);
            var tagEntity = tagsByName[cleanedTag];

            var alreadyExists = subscription.SubscriptionTags
                .Any(x => x.TagId == tagEntity.Id);

            if (!alreadyExists)
            {
                dbContext.SubscriptionTags.Add(new SubscriptionTagEntity { SubscriptionId = subscription.Id, TagId = tagEntity.Id });

                await dbContext.SaveChangesAsync(ct);
            }

            await dbContext.Entry(subscription)
                .Collection(x => x.SubscriptionTags)
                .Query()
                .Include(x => x.Tag)
                .LoadAsync(ct);

            await transaction.CommitAsync(ct);
            return ToRecord(subscription);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<bool> TryRenameTagAsync(long chatId, string tag, string newTag, CancellationToken ct = default)
    {
        var cleanedOldTag = tag.Trim();
        var cleanedNewTag = newTag.Trim();

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(ct);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);

        try
        {
            var subscriptions = await dbContext.Subscriptions
                .Include(x => x.SubscriptionTags)
                .ThenInclude(x => x.Tag)
                .Where(x => x.ChatId == chatId)
                .Where(x => x.SubscriptionTags.Any(st => st.Tag.Name == cleanedOldTag))
                .ToListAsync(ct);

            if (subscriptions.Count == 0)
            {
                await transaction.RollbackAsync(ct);
                return false;
            }

            var tagsByName = await OrmTagsHelper.GetOrCreateTagsAsync(dbContext, [cleanedNewTag], ct);
            var newTagEntity = tagsByName[cleanedNewTag];

            foreach (var subscription in subscriptions)
            {
                var oldRelations = subscription.SubscriptionTags
                    .Where(x => x.Tag.Name == cleanedOldTag)
                    .ToArray();

                var alreadyHasNewTag = subscription.SubscriptionTags
                    .Any(x => x.TagId == newTagEntity.Id);

                if (!alreadyHasNewTag)
                {
                    dbContext.SubscriptionTags.Add(new SubscriptionTagEntity { SubscriptionId = subscription.Id, TagId = newTagEntity.Id });
                }

                dbContext.SubscriptionTags.RemoveRange(oldRelations);
            }

            await dbContext.SaveChangesAsync(ct);
            await OrmTagsHelper.DeleteOrphanTagByNameAsync(dbContext, cleanedOldTag, ct);

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
        var cleanedTag = tag.Trim();

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(ct);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);

        try
        {
            var relations = await dbContext.SubscriptionTags
                .Include(x => x.Subscription)
                .Include(x => x.Tag)
                .Where(x => x.Subscription.ChatId == chatId && x.Tag.Name == cleanedTag)
                .ToListAsync(ct);

            if (relations.Count == 0)
            {
                await transaction.RollbackAsync(ct);
                return false;
            }

            dbContext.SubscriptionTags.RemoveRange(relations);
            await dbContext.SaveChangesAsync(ct);

            await OrmTagsHelper.DeleteOrphanTagByNameAsync(dbContext, cleanedTag, ct);

            await transaction.CommitAsync(ct);
            return true;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<IReadOnlyList<TrackedLinkSubscription>> GetAllSubscriptionsAsync(CancellationToken ct = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(ct);

        return await dbContext.Subscriptions
            .AsNoTracking()
            .GroupBy(x => new { x.LinkId, x.Link.Url, x.Link.LastUpdatedAt })
            .Select(group => new TrackedLinkSubscription
            {
                Id = group.Key.LinkId,
                Url = new Uri(group.Key.Url),
                LastUpdatedAt = group.Key.LastUpdatedAt,
                LastEventKey = group.First().Link.LastEventKey,
                TgChatIds = group.Select(x => x.ChatId).Distinct().ToArray()
            })
            .ToArrayAsync(ct);
    }

    public async Task<IReadOnlyList<TrackedLinkSubscription>> GetSubscriptionsBatchAsync(
        long? afterLinkId,
        int batchSize,
        CancellationToken ct = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(ct);

        return await dbContext.Links
            .AsNoTracking()
            .Where(x => x.Subscriptions.Any())
            .Where(x => afterLinkId == null || x.Id > afterLinkId.Value)
            .OrderBy(x => x.Id)
            .Take(batchSize)
            .Select(x => new TrackedLinkSubscription
            {
                Id = x.Id,
                Url = new Uri(x.Url),
                LastUpdatedAt = x.LastUpdatedAt,
                LastEventKey = x.LastEventKey,
                TgChatIds = x.Subscriptions
                    .Select(s => s.ChatId)
                    .Distinct()
                    .ToArray()
            })
            .ToArrayAsync(ct);
    }

    public async Task SetCursorAsync(long linkId, DateTimeOffset lastUpdatedAt, string? lastEventKey, CancellationToken ct = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(ct);

        var entity = await dbContext.Links
            .FirstOrDefaultAsync(x => x.Id == linkId, ct);

        if (entity is null)
        {
            return;
        }

        entity.LastUpdatedAt = lastUpdatedAt;
        entity.LastEventKey = lastEventKey;
        await dbContext.SaveChangesAsync(ct);
    }

    private static TrackedLinkRecord ToRecord(SubscriptionEntity subscription)
    {
        return new TrackedLinkRecord
        {
            Id = subscription.LinkId,
            Url = new Uri(subscription.Link.Url),
            LastUpdatedAt = subscription.Link.LastUpdatedAt,
            LastEventKey = subscription.Link.LastEventKey,
            Tags = subscription.SubscriptionTags
                .Select(x => x.Tag.Name)
                .Distinct()
                .OrderBy(x => x)
                .ToArray(),
        };
    }
}