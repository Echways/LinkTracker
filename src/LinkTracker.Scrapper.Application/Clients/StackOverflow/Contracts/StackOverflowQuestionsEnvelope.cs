using System.Text.Json.Serialization;

namespace LinkTracker.Scrapper.Application.Clients.StackOverflow.Contracts;

public sealed class StackOverflowQuestionsEnvelope
{
    [JsonPropertyName("items")] public IReadOnlyList<StackOverflowQuestionResponse> Items { get; init; } = [];
}