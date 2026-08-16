using Docker.DotNet;
using LinkTracker.Scrapper.Infrastructure.Configuration.Database;
using LinkTracker.Scrapper.Infrastructure.Database.Migrations;
using LinkTracker.Scrapper.Storage.Orm;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Testcontainers.PostgreSql;

namespace LinkTracker.Tests.Scrapper.Integration.Storage;

public sealed class PostgresSqlStorageFixture : IAsyncLifetime
{
    private const string DatabaseName = "linktracker_test";
    private const string Username = "postgres";
    private const string Password = "postgres";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase(DatabaseName)
        .WithUsername(Username)
        .WithPassword(Password)
        .Build();

    public string ConnectionString { get; private set; } = string.Empty;

    public NpgsqlDataSource DataSource { get; private set; } = default!;

    public async Task InitializeAsync()
    {
        const int maxAttempts = 3;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await _container.StartAsync();
                break;
            }
            catch (DockerApiException) when (attempt < maxAttempts)
            {
                await Task.Delay(TimeSpan.FromSeconds(5 * attempt));
            }
        }

        ConnectionString = _container.GetConnectionString();

        await WaitUntilDatabaseReadyAsync(ConnectionString);

        DataSource = new NpgsqlDataSourceBuilder(ConnectionString).Build();

        var options = new DatabaseOptions
        {
            Host = _container.Hostname,
            Port = _container.GetMappedPublicPort(PostgreSqlBuilder.PostgreSqlPort),
            Name = DatabaseName,
            User = Username,
            Password = Password,
            AccessType = DatabaseAccessType.Sql,
            RunMigrations = true,
            MigrationsPath = "../../migrations"
        };

        var env = new TestWebHostEnvironment
        {
            ContentRootPath = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LinkTracker.Scrapper"))
        };

        var migrator = new DbUpMigrator(
            Options.Create(options),
            NullLogger<DbUpMigrator>.Instance,
            env);

        await migrator.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await DataSource.DisposeAsync();
        await _container.DisposeAsync();
    }

    public AppDbContext CreateDbContext()
    {
        return new AppDbContext(BuildDbContextOptions());
    }

    public IDbContextFactory<AppDbContext> CreateDbContextFactory()
    {
        return new TestDbContextFactory(BuildDbContextOptions());
    }

    private DbContextOptions<AppDbContext> BuildDbContextOptions()
    {
        return new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(DataSource)
            .Options;
    }

    public async Task ResetAsync()
    {
        const string sql =
            """
            truncate table
                outbox_messages,
                subscription_tags,
                subscriptions,
                tags,
                links,
                chats
            restart identity cascade;
            """;

        await using var command = DataSource.CreateCommand(sql);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task WaitUntilDatabaseReadyAsync(string connectionString)
    {
        const int maxAttempts = 10;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync();

                await using var command = new NpgsqlCommand("select 1", connection);
                await command.ExecuteScalarAsync();

                return;
            }
            catch (NpgsqlException) when (attempt < maxAttempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500 * attempt));
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500 * attempt));
            }
        }

        throw new InvalidOperationException("PostgreSQL did not become ready in time.");
    }

    private sealed class TestDbContextFactory(DbContextOptions<AppDbContext> options)
        : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext()
        {
            return new AppDbContext(options);
        }
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "LinkTracker.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = default!;
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = default!;
    }
}