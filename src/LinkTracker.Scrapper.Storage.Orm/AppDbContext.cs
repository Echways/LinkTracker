using LinkTracker.Scrapper.Storage.Orm.Entities;
using Microsoft.EntityFrameworkCore;

namespace LinkTracker.Scrapper.Storage.Orm;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<ChatEntity> Chats => Set<ChatEntity>();
    public DbSet<LinkEntity> Links => Set<LinkEntity>();
    public DbSet<SubscriptionEntity> Subscriptions => Set<SubscriptionEntity>();
    public DbSet<TagEntity> Tags => Set<TagEntity>();
    public DbSet<SubscriptionTagEntity> SubscriptionTags => Set<SubscriptionTagEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}