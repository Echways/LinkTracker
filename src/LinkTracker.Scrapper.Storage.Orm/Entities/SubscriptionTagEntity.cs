namespace LinkTracker.Scrapper.Storage.Orm.Entities;

public sealed class SubscriptionTagEntity
{
    public long SubscriptionId { get; set; }

    public SubscriptionEntity Subscription { get; set; } = default!;

    public long TagId { get; set; }

    public TagEntity Tag { get; set; } = default!;
}