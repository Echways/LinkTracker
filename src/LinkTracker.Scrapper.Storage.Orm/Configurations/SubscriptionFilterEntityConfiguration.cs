using LinkTracker.Scrapper.Storage.Orm.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinkTracker.Scrapper.Storage.Orm.Configurations;

public sealed class SubscriptionFilterEntityConfiguration : IEntityTypeConfiguration<SubscriptionFilterEntity>
{
    public void Configure(EntityTypeBuilder<SubscriptionFilterEntity> builder)
    {
        builder.ToTable("subscription_filters");

        builder.HasKey(x => new { x.SubscriptionId, x.FilterId });

        builder.Property(x => x.SubscriptionId)
            .HasColumnName("subscription_id");

        builder.Property(x => x.FilterId)
            .HasColumnName("filter_id");

        builder.HasOne(x => x.Subscription)
            .WithMany(x => x.SubscriptionFilters)
            .HasForeignKey(x => x.SubscriptionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Filter)
            .WithMany(x => x.SubscriptionFilters)
            .HasForeignKey(x => x.FilterId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}