namespace LinkTracker.Scrapper.Storage.Orm.Entities;

public sealed class SubscriptionEntity
{
    public long Id { get; set; }

    public long ChatId { get; set; }

    public ChatEntity Chat { get; set; } = default!;

    public long LinkId { get; set; }

    public LinkEntity Link { get; set; } = default!;

    public ICollection<SubscriptionTagEntity> SubscriptionTags { get; set; } = [];

}