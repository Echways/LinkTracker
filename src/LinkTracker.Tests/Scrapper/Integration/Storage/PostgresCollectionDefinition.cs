namespace LinkTracker.Tests.Scrapper.Integration.Storage;

[CollectionDefinition("Postgres collection", DisableParallelization = true)]
public sealed class PostgresCollectionDefinition : ICollectionFixture<PostgresSqlStorageFixture>
{
}