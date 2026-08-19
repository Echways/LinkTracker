using Npgsql;

namespace LinkTracker.Scrapper.Infrastructure.Configuration.Database;

public sealed class DatabaseOptions
{
    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 5432;
    public string Name { get; init; } = "linktracker";
    public string User { get; init; } = "postgres";
    public string Password { get; init; } = string.Empty;
    public DatabaseAccessType AccessType { get; init; } = DatabaseAccessType.Orm;
    public string MigrationsPath { get; init; } = "migrations";
    public bool RunMigrations { get; init; } = true;
    public SslMode SslMode { get; init; } = SslMode.Disable;

    public string BuildConnectionString()
    {
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = Host,
            Port = Port,
            Database = Name,
            Username = User,
            Password = Password,
            SslMode = SslMode
        };

        return builder.ConnectionString;
    }
}

public enum DatabaseAccessType
{
    InMemory,
    Sql,
    Orm
}