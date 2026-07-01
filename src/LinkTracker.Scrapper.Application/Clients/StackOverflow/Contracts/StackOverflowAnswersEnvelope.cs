using System.Text.Json.Serialization;

namespace LinkTracker.Scrapper.Application.Clients.StackOverflow.Contracts;

public sealed class StackOverflowAnswersEnvelope
{
    [JsonPropertyName("items")] public IReadOnlyList<StackOverflowAnswerResponse> Items { get; init; } = [];
}