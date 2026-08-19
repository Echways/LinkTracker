using LinkTracker.Bot.Application.Commands.Registration;
using LinkTracker.Bot.Application.Dialogs.Registration;
using LinkTracker.Bot.Application.Routing.Registration;
using LinkTracker.Bot.Infrastructure.Clients.Registration;
using LinkTracker.Bot.Infrastructure.Storage.Registration;
using LinkTracker.Bot.Infrastructure.Telemetry.Registration;
using LinkTracker.Bot.Presentation.BotApi.Endpoints;
using LinkTracker.Bot.Presentation.BotApi.Registration;
using LinkTracker.Bot.Presentation.Grpc;
using LinkTracker.Bot.Presentation.Telegram.Registration;
using LinkTracker.EnvReader;
using LinkTracker.Shared.Infrastructure.Authentication;
using LinkTracker.Shared.Infrastructure.Logging;
using LinkTracker.Shared.Infrastructure.RateLimiting;
using LinkTracker.Shared.Infrastructure.Resilience;
using LinkTracker.Shared.Infrastructure.Telemetry;

var builder = WebApplication.CreateBuilder(args);

builder.AddLocalDotEnv();
builder.AddSharedSerilog("bot");

builder.Services.AddGrpc();
builder.Services.AddHttpResilienceOptions(builder.Configuration);
builder.Services.AddApiRateLimiting(builder.Configuration);
builder.Services.AddServiceAuthentication(builder.Configuration);
builder.Services.AddServiceAuthClients(builder.Configuration);

builder.Services.AddCommands();
builder.Services.AddUpdateRouting();
builder.Services.AddDialogs();
builder.Services.AddDialogStorage();
builder.Services.AddClients(builder.Configuration);
builder.Services.AddTelegramPresentation(builder.Configuration);
builder.Services.AddTelegramNotifications();
builder.Services.AddBotApi();
builder.Services.AddTelemetry(builder.Configuration);

var app = builder.Build();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapBotApi()
    .RequireServiceAuthorization()
    .RequireRateLimiting(RateLimitingPolicies.PublicApi);

app.MapGrpcService<BotUpdatesGrpcService>()
    .RequireServiceAuthorization();

app.MapMetricsEndpoint().RequireHost("*:8011");

await app.RunAsync();
