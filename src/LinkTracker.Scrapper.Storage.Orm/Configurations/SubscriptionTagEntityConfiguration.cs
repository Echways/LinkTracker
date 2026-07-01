using LinkTracker.Scrapper.Storage.Orm.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinkTracker.Scrapper.Storage.Orm.Configurations;

public sealed class SubscriptionTagEntityConfiguration : IEntityTypeConfiguration<SubscriptionTagEntity>
{
    public void Configure(EntityTypeBuilder<SubscriptionTagEntity> builder)
    {
        builder.ToTable("subscription_tags");

        builder.HasKey(x => new { x.SubscriptionId, x.TagId });

        builder.Property(x => x.SubscriptionId)
            .HasColumnName("subscription_id");

        builder.Property(x => x.TagId)
            .HasColumnName("tag_id");

        builder.HasOne(x => x.Subscription)
            .WithMany(x => x.SubscriptionTags)
            .HasForeignKey(x => x.SubscriptionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Tag)
            .WithMany(x => x.SubscriptionTags)
            .HasForeignKey(x => x.TagId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}