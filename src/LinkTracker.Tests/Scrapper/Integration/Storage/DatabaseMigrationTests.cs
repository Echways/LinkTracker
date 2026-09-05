using Npgsql;

namespace LinkTracker.Tests.Scrapper.Integration.Storage;

[Trait("Module", "Scrapper")]
[Trait("Category", "Integration")]
[Collection("Postgres collection")]
public sealed class DatabaseMigrationTests(PostgresSqlStorageFixture fixture)
{
    [Fact]
    public async Task Migrations_CreateExpectedSchema()
    {
        await using var connection = await fixture.DataSource.OpenConnectionAsync();

        var tables = await GetTableNamesAsync(connection);
        var indexes = await GetIndexNamesAsync(connection);

        Assert.Contains("chats", tables);
        Assert.Contains("links", tables);
        Assert.Contains("subscriptions", tables);
        Assert.Contains("tags", tables);
        Assert.Contains("subscription_tags", tables);
        Assert.Contains("dbup_schema_versions", tables);
        Assert.Contains("outbox_messages", tables);

        Assert.DoesNotContain("filters", tables);
        Assert.DoesNotContain("subscription_filters", tables);

        Assert.Contains("ix_subscriptions_chat_id", indexes);
        Assert.Contains("ix_subscriptions_link_id", indexes);
        Assert.Contains("ix_links_normalized_url", indexes);
        Assert.Contains("ix_tags_name", indexes);
        Assert.Contains("ix_outbox_messages_unprocessed", indexes);
    }

    [Fact]
    public async Task Migrations_CreateExpectedConstraints()
    {
        await using var connection = await fixture.DataSource.OpenConnectionAsync();

        var uniqueConstraints = await GetUniqueConstraintNamesAsync(connection);

        Assert.Contains("links_normalized_url_key", uniqueConstraints);
        Assert.Contains("subscriptions_chat_id_link_id_key", uniqueConstraints);
        Assert.Contains("tags_name_key", uniqueConstraints);
        Assert.Contains("subscription_tags_pkey", uniqueConstraints);
    }

    private static async Task<HashSet<string>> GetTableNamesAsync(NpgsqlConnection connection)
    {
        const string sql =
            """
            select table_name
            from information_schema.tables
            where table_schema = 'public';
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        var result = new HashSet<string>(StringComparer.Ordinal);

        while (await reader.ReadAsync())
        {
            result.Add(reader.GetString(0));
        }

        return result;
    }

    private static async Task<HashSet<string>> GetIndexNamesAsync(NpgsqlConnection connection)
    {
        const string sql =
            """
            select indexname
            from pg_indexes
            where schemaname = 'public';
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        var result = new HashSet<string>(StringComparer.Ordinal);

        while (await reader.ReadAsync())
        {
            result.Add(reader.GetString(0));
        }

        return result;
    }

    private static async Task<HashSet<string>> GetUniqueConstraintNamesAsync(NpgsqlConnection connection)
    {
        const string sql =
            """
            select constraint_name
            from information_schema.table_constraints
            where table_schema = 'public'
              and constraint_type in ('UNIQUE', 'PRIMARY KEY');
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        var result = new HashSet<string>(StringComparer.Ordinal);

        while (await reader.ReadAsync())
        {
            result.Add(reader.GetString(0));
        }

        return result;
    }
}