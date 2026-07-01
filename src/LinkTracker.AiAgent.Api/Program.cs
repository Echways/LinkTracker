using LinkTracker.AiAgent.Application.Registration;
using LinkTracker.AiAgent.Infrastructure.Clients.Registration;
using LinkTracker.EnvReader;

var builder = WebApplication.CreateBuilder(args);

builder.AddLocalDotEnv();

builder.Services.AddAiAgentApplication();
builder.Services.AddAiAgentInfrastructure(builder.Configuration);

var app = builder.Build();

app.MapGet("/health", () => Results.Ok());

await app.RunAsync();