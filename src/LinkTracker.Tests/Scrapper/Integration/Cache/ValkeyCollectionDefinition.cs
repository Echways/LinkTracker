namespace LinkTracker.Tests.Scrapper.Integration.Cache;

[CollectionDefinition("Valkey collection", DisableParallelization = true)]
public sealed class ValkeyCollectionDefinition : ICollectionFixture<ValkeyTestContainerFixture>
{
}