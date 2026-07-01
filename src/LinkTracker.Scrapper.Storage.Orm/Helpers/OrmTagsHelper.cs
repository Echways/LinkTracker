using LinkTracker.Scrapper.Storage.Orm.Entities;
using Microsoft.EntityFrameworkCore;

namespace LinkTracker.Scrapper.Storage.Orm.Helpers;

internal static class OrmTagsHelper
{
    public static async Task<Dictionary<string, TagEntity>> GetOrCreateTagsAsync(
        AppDbContext dbContext,
        IReadOnlyCollection<string> tagNames,
        CancellationToken ct)
    {
        var normalizedTagNames = tagNames
            .Where(static x => string.IsNullOrWhiteSpace(x) is false)
            .Select(static x => x.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var existingTags = await dbContext.Tags
            .Where(x => normalizedTagNames.Contains(x.Name))
            .ToDictionaryAsync(x => x.Name, StringComparer.Ordinal, ct);

        var missingTagNames = normalizedTagNames
            .Where(tagName => !existingTags.ContainsKey(tagName))
            .ToArray();

        if (missingTagNames.Length > 0)
        {
            foreach (var tagName in missingTagNames)
            {
                var tagEntity = new TagEntity { Name = tagName };

                dbContext.Tags.Add(tagEntity);
                existingTags[tagName] = tagEntity;
            }

            await dbContext.SaveChangesAsync(ct);
        }

        return existingTags;
    }

    public static async Task DeleteOrphanTagByNameAsync(
        AppDbContext dbContext,
        string tagName,
        CancellationToken ct)
    {
        var tagEntity = await dbContext.Tags
            .Include(x => x.SubscriptionTags)
            .FirstOrDefaultAsync(x => x.Name == tagName, ct);

        if (tagEntity is null || tagEntity.SubscriptionTags.Count != 0)
        {
            return;
        }

        dbContext.Tags.Remove(tagEntity);
    }
}