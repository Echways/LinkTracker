using LinkTracker.Scrapper.Infrastructure.Configuration.Database;
using LinkTracker.Scrapper.Storage.Abstractions.Models;
using LinkTracker.Scrapper.Storage.Orm;
using LinkTracker.Scrapper.Storage.Sql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LinkTracker.Scrapper.Infrastructure.Storage.Registration;

public static class StorageModule
{
    public static IServiceCollection AddStorage(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var databaseSection = configuration.GetSection("Database");

        if (!databaseSection.Exists())
        {
            throw new InvalidOperationException("Секция 'Database' не найдена.");
        }

        services.Configure<DatabaseOptions>(databaseSection);

        var databaseOptions = databaseSection.Get<DatabaseOptions>()
                              ?? throw new InvalidOperationException("Не удалось прочитать настройки базы данных.");

        switch (databaseOptions.AccessType)
        {
            case DatabaseAccessType.Sql:
                services.AddSingleton<ILinkTrackingStore, SqlLinkTrackingStore>();
                break;
            case DatabaseAccessType.Orm:
                services.AddSingleton<ILinkTrackingStore, OrmLinkTrackingStore>();
                break;
            default:
                throw new InvalidOperationException(
                    $"Неподдерживаемый тип доступа к БД: '{databaseOptions.AccessType}'.");
        }

        return services;
    }
}