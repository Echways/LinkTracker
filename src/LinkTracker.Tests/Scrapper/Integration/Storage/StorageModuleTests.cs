using LinkTracker.Scrapper.Infrastructure.Storage.Registration;
using LinkTracker.Scrapper.Storage.Abstractions.Models;
using LinkTracker.Scrapper.Storage.Orm;
using LinkTracker.Scrapper.Storage.Sql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace LinkTracker.Tests.Scrapper.Integration.Storage;

[Trait("Module", "Scrapper")]
[Trait("Category", "Integration")]
[Collection("Postgres collection")]
public sealed class StorageModuleTests(PostgresSqlStorageFixture fixture)
{
    [Theory]
    [InlineData("SQL", typeof(SqlLinkTrackingStore))]
    [InlineData("ORM", typeof(OrmLinkTrackingStore))]
    public void AddStorage_ResolvesExpectedImplementation(
        string accessType,
        Type expectedImplementationType)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Database:AccessType"] = accessType, ["ConnectionStrings:Postgres"] = fixture.ConnectionString })
            .Build();

        var services = new ServiceCollection();

        services.AddSingleton<IConfiguration>(configuration);

        services.AddSingleton<NpgsqlDataSource>(_ =>
            new NpgsqlDataSourceBuilder(fixture.ConnectionString).Build());

        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(fixture.ConnectionString);
        });

        services.AddStorage(configuration);

        using var serviceProvider = services.BuildServiceProvider();

        var store = serviceProvider.GetRequiredService<ILinkTrackingStore>();

        Assert.NotNull(store);
        Assert.IsType(expectedImplementationType, store);
    }

    [Theory]
    [InlineData("SQL")]
    [InlineData("ORM")]
    public async Task AddStorage_ResolvedStore_IsUsable(string accessType)
    {
        await fixture.ResetAsync();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Database:AccessType"] = accessType, ["ConnectionStrings:Postgres"] = fixture.ConnectionString })
            .Build();

        var services = new ServiceCollection();

        services.AddSingleton<IConfiguration>(configuration);

        services.AddSingleton<NpgsqlDataSource>(_ =>
            new NpgsqlDataSourceBuilder(fixture.ConnectionString).Build());

        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(fixture.ConnectionString);
        });

        services.AddStorage(configuration);

        await using var serviceProvider = services.BuildServiceProvider().CreateAsyncScope();

        var store = serviceProvider.ServiceProvider.GetRequiredService<ILinkTrackingStore>();

        const long chatId = 990001;

        var registered = await store.TryRegisterChatAsync(chatId);
        var exists = await store.ChatExistsAsync(chatId);

        Assert.True(registered);
        Assert.True(exists);
    }
}