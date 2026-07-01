namespace LinkTracker.Scrapper.Storage.Orm.Entities;

public sealed class ChatEntity
{
    public long Id { get; set; }

    public ICollection<SubscriptionEntity> Subscriptions { get; set; } = [];
}