using System.Text.Json.Serialization;

namespace LinkTracker.Scrapper.Application.Clients.StackOverflow.Contracts;

public sealed class StackOverflowUserResponse
{
    [JsonPropertyName("display_name")] public string DisplayName { get; init; } = string.Empty;
}