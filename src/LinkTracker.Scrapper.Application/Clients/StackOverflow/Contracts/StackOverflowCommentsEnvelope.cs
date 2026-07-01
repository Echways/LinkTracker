using System.Text.Json.Serialization;

namespace LinkTracker.Scrapper.Application.Clients.StackOverflow.Contracts;

public sealed class StackOverflowCommentsEnvelope
{
    [JsonPropertyName("items")] public IReadOnlyList<StackOverflowCommentResponse> Items { get; init; } = [];
}