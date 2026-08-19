using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;

namespace LinkTracker.Scrapper.Infrastructure.OpenApi;

public static class OpenApiApplicationBuilderExtensions
{
    public static IApplicationBuilder UseScrapperOpenApi(
        this IApplicationBuilder app,
        IHostEnvironment environment)
    {
        if (!environment.IsDevelopment())
        {
            return app;
        }

        app.UseOpenApi(settings =>
        {
            settings.Path = "/swagger/{documentName}/swagger.json";
        });

        app.UseSwaggerUi(settings =>
        {
            settings.Path = "/swagger";
            settings.DocumentPath = "/swagger/{documentName}/swagger.json";
        });

        return app;
    }
}
