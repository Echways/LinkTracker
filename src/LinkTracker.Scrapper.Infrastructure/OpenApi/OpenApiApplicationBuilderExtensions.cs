using Microsoft.AspNetCore.Builder;

namespace LinkTracker.Scrapper.Infrastructure.OpenApi;

public static class OpenApiApplicationBuilderExtensions
{
    public static IApplicationBuilder UseScrapperOpenApi(this IApplicationBuilder app)
    {
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