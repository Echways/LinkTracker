using LinkTracker.Scrapper.Storage.Orm.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinkTracker.Scrapper.Storage.Orm.Configurations;

public sealed class LinkEntityConfiguration : IEntityTypeConfiguration<LinkEntity>
{
    public void Configure(EntityTypeBuilder<LinkEntity> builder)
    {
        builder.ToTable("links");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.Url)
            .HasColumnName("url")
            .IsRequired();

        builder.Property(x => x.NormalizedUrl)
            .HasColumnName("normalized_url")
            .IsRequired();

        builder.Property(x => x.LastUpdatedAt)
            .HasColumnName("last_updated_at");

        builder.Property(x => x.LastEventKey)
            .HasColumnName("last_event_key");

        builder.Property(x => x.LastCheckedAt)
            .HasColumnName("last_checked_at");

        builder.HasIndex(x => x.NormalizedUrl)
            .IsUnique();

        builder.HasIndex(x => new { x.LastCheckedAt, x.Id })
            .HasDatabaseName("ix_links_last_checked_at");
    }
}