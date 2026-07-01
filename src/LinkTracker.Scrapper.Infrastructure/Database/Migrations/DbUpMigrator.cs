using DbUp;
using DbUp.ScriptProviders;
using LinkTracker.Scrapper.Infrastructure.Configuration.Database;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LinkTracker.Scrapper.Infrastructure.Database.Migrations;

public sealed class DbUpMigrator(
    IOptions<DatabaseOptions> options,
    ILogger<DbUpMigrator> logger,
    IWebHostEnvironment environment)
{
    private readonly DatabaseOptions _options = options.Value;

    public Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        if (ShouldSkipMigrations())
        {
            logger.LogInformation("Пропускаю миграции базы данных.");
            return Task.CompletedTask;
        }

        var connectionString = _options.BuildConnectionString();

        var migrationsPath = Path.GetFullPath(
            Path.Combine(environment.ContentRootPath, _options.MigrationsPath));

        if (Directory.Exists(migrationsPath) is false)
        {
            throw new DirectoryNotFoundException(
                $"Директория миграций не была найдена: {migrationsPath}");
        }

        EnsureDatabase.For.PostgresqlDatabase(connectionString);

        var upgrader = DeployChanges.To
            .PostgresqlDatabase(connectionString)
            .JournalToPostgresqlTable("public", "dbup_schema_versions")
            .WithScriptsFromFileSystem(
                migrationsPath,
                new FileSystemScriptOptions { IncludeSubDirectories = false })
            .Build();

        var result = upgrader.PerformUpgrade();

        if (result.Successful is false)
        {
            logger.LogError(result.Error, "Ошибка применения миграций");
            throw result.Error ?? new InvalidOperationException("DbUp migration failed");
        }

        logger.LogInformation("Миграции успешно применены.");
        return Task.CompletedTask;
    }

    private bool ShouldSkipMigrations()
    {
        if (_options.RunMigrations is false)
        {
            return true;
        }

        return Equals(
            _options.AccessType,
            DatabaseAccessType.InMemory);
    }
}