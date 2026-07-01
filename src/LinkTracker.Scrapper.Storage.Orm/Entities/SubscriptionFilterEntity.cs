namespace LinkTracker.Scrapper.Storage.Orm.Entities;

public sealed class SubscriptionFilterEntity
{
    public long SubscriptionId { get; set; }

    public SubscriptionEntity Subscription { get; set; } = default!;

    public long FilterId { get; set; }

    public FilterEntity Filter { get; set; } = default!;
}