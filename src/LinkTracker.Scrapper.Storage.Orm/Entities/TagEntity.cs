namespace LinkTracker.Scrapper.Storage.Orm.Entities;

public sealed class TagEntity
{
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public ICollection<SubscriptionTagEntity> SubscriptionTags { get; set; } = [];
}