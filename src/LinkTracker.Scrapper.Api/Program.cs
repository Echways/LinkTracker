using LinkTracker.EnvReader;
using LinkTracker.Scrapper.Application.Registration;
using LinkTracker.Scrapper.Infrastructure.Cache.Registration;
using LinkTracker.Scrapper.Infrastructure.Clients.Registration;
using LinkTracker.Scrapper.Infrastructure.Database.Migrations;
using LinkTracker.Scrapper.Infrastructure.Database.Registration;
using LinkTracker.Scrapper.Infrastructure.Errors;
using LinkTracker.Scrapper.Infrastructure.OpenApi;
using LinkTracker.Scrapper.Infrastructure.Outbox.Registration;
using LinkTracker.Scrapper.Infrastructure.Quartz.Registration;
using LinkTracker.Scrapper.Infrastructure.Storage.Registration;
using LinkTracker.Scrapper.Infrastructure.Telemetry.Middleware;
using LinkTracker.Scrapper.Infrastructure.Telemetry.Registration;
using LinkTracker.Scrapper.Presentation.Endpoints;
using LinkTracker.Scrapper.Presentation.Grpc;
using LinkTracker.Shared.Infrastructure.RateLimiting;
using LinkTracker.Shared.Infrastructure.Resilience;
using LinkTracker.Shared.Infrastructure.Telemetry;

var builder = WebApplication.CreateBuilder(args);

builder.AddLocalDotEnv();

builder.Services.AddGrpc(options =>
{
    options.Interceptors.Add<GrpcExceptionInterceptor>();
});
builder.Services.AddHttpResilienceOptions(builder.Configuration);
builder.Services.AddIpRateLimiting(builder.Configuration);

builder.Services.AddScrapperOpenApi();
builder.Services.AddDatabase(builder.Configuration);
builder.Services.AddStorage(builder.Configuration);
builder.Services.AddCache(builder.Configuration);
builder.Services.AddApplication();
builder.Services.AddClients(builder.Configuration);
builder.Services.AddOutbox(builder.Configuration);
builder.Services.AddQuartzScheduling(builder.Configuration);
builder.Services.AddTelemetry(builder.Configuration);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var migrator = scope.ServiceProvider.GetRequiredService<DbUpMigrator>();
    await migrator.MigrateAsync();
}

app.UseScrapperExceptionHandling();
app.UseScrapperOpenApi();
app.UseRateLimiter();

app.UseMiddleware<ApiRequestsMiddleware>();
app.UseMiddleware<RequestDurationMiddleware>();

app.MapScrapperEndpoints();
app.MapGrpcService<ScrapperGrpcService>();

app.MapMetricsEndpoint();
await app.RunAsync();