using LinkTracker.AiAgent.Application.Registration;
using LinkTracker.AiAgent.Infrastructure.Clients.Registration;
using LinkTracker.AiAgent.Infrastructure.Telemetry.Registration;
using LinkTracker.EnvReader;
using LinkTracker.Shared.Infrastructure.Telemetry;

var builder = WebApplication.CreateBuilder(args);

builder.AddLocalDotEnv();

builder.Services.AddAiAgentApplication();
builder.Services.AddAiAgentInfrastructure(builder.Configuration);
builder.Services.AddTelemetry(builder.Configuration);

var app = builder.Build();

app.MapGet("/health", () => Results.Ok());

app.MapMetricsEndpoint().RequireHost("*:8102");

await app.RunAsync();
