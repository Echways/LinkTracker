using LinkTracker.Scrapper.Storage.Orm.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinkTracker.Scrapper.Storage.Orm.Configurations;

public sealed class FilterEntityConfiguration : IEntityTypeConfiguration<FilterEntity>
{
    public void Configure(EntityTypeBuilder<FilterEntity> builder)
    {
        builder.ToTable("filters");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.Value)
            .HasColumnName("value")
            .IsRequired();

        builder.HasIndex(x => x.Value)
            .IsUnique();
    }
}