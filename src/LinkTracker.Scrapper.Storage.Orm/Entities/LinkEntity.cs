namespace LinkTracker.Scrapper.Storage.Orm.Entities;

public sealed class LinkEntity
{
    public long Id { get; set; }

    public string Url { get; set; } = string.Empty;

    public string NormalizedUrl { get; set; } = string.Empty;

    public DateTimeOffset? LastUpdatedAt { get; set; }
    public string? LastEventKey { get; set; }
    public DateTimeOffset LastCheckedAt { get; set; } = DateTimeOffset.MinValue;

    public ICollection<SubscriptionEntity> Subscriptions { get; set; } = [];
}