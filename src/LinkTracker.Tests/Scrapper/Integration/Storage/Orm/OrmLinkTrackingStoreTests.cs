using LinkTracker.Scrapper.Storage.Abstractions.Models;
using LinkTracker.Scrapper.Storage.Orm;

namespace LinkTracker.Tests.Scrapper.Integration.Storage.Orm;

[Trait("Module", "Scrapper")]
[Trait("Category", "Integration")]
[Collection("Postgres collection")]
public sealed class OrmLinkTrackingStoreTests(PostgresSqlStorageFixture fixture)
    : LinkTrackingStoreContractTests
{
    protected override async Task ExecuteWithSut(Func<ILinkTrackingStore, Task> test)
    {
        await fixture.ResetAsync();

        await using var dbContext = fixture.CreateDbContext();
        ILinkTrackingStore sut = new OrmLinkTrackingStore(dbContext);

        await test(sut);
    }
}