using Microsoft.Extensions.DependencyInjection;

namespace LinkTracker.Scrapper.Infrastructure.OpenApi;

public static class OpenApiModule
{
    public static IServiceCollection AddScrapperOpenApi(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();

        services.AddOpenApiDocument(options =>
        {
            options.DocumentName = "v1";
            options.Title = "LinkTracker Scrapper API";
            options.Version = "v1";
            options.Description = "REST API for chat registration and link tracking.";
        });

        return services;
    }
}