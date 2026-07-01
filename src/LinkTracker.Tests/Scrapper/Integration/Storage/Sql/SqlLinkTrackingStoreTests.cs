using LinkTracker.Scrapper.Storage.Abstractions.Models;
using LinkTracker.Scrapper.Storage.Sql;

namespace LinkTracker.Tests.Scrapper.Integration.Storage.Sql;

[Trait("Module", "Scrapper")]
[Trait("Category", "Integration")]
[Collection("Postgres collection")]
public sealed class SqlLinkTrackingStoreTests(PostgresSqlStorageFixture fixture)
    : LinkTrackingStoreContractTests
{
    protected override async Task ExecuteWithSut(Func<ILinkTrackingStore, Task> test)
    {
        await fixture.ResetAsync();

        ILinkTrackingStore sut = new SqlLinkTrackingStore(fixture.DataSource);
        await test(sut);
    }
}