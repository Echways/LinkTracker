using LinkTracker.Scrapper.Storage.Orm.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinkTracker.Scrapper.Storage.Orm.Configurations;

public sealed class SubscriptionEntityConfiguration : IEntityTypeConfiguration<SubscriptionEntity>
{
    public void Configure(EntityTypeBuilder<SubscriptionEntity> builder)
    {
        builder.ToTable("subscriptions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.ChatId)
            .HasColumnName("chat_id");

        builder.Property(x => x.LinkId)
            .HasColumnName("link_id");

        builder.HasIndex(x => new { x.ChatId, x.LinkId })
            .IsUnique();

        builder.HasOne(x => x.Chat)
            .WithMany(x => x.Subscriptions)
            .HasForeignKey(x => x.ChatId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Link)
            .WithMany(x => x.Subscriptions)
            .HasForeignKey(x => x.LinkId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}