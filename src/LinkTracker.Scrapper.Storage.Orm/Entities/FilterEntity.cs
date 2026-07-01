namespace LinkTracker.Scrapper.Storage.Orm.Entities;

public sealed class FilterEntity
{
    public long Id { get; set; }

    public string Value { get; set; } = string.Empty;

    public ICollection<SubscriptionFilterEntity> SubscriptionFilters { get; set; } = [];
}