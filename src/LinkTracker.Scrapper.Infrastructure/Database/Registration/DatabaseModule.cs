using LinkTracker.Scrapper.Infrastructure.Configuration.Database;
using LinkTracker.Scrapper.Infrastructure.Database.Migrations;
using LinkTracker.Scrapper.Storage.Orm;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;

namespace LinkTracker.Scrapper.Infrastructure.Database.Registration;

public static class DatabaseModule
{
    public static IServiceCollection AddDatabase(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<DatabaseOptions>(configuration.GetSection("Database"));

        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            return new NpgsqlDataSourceBuilder(options.BuildConnectionString()).Build();
        });

        services.AddDbContextFactory<AppDbContext>((sp, options) =>
        {
            var databaseOptions = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            options.UseNpgsql(databaseOptions.BuildConnectionString());
        });

        services.AddSingleton<DbUpMigrator>();

        return services;
    }
}