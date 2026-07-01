using System.Text.Json.Serialization;

namespace LinkTracker.Scrapper.Application.Clients.StackOverflow.Contracts;

public sealed class StackOverflowQuestionResponse
{
    [JsonPropertyName("question_id")] public long QuestionId { get; init; }

    [JsonPropertyName("title")] public string Title { get; init; } = string.Empty;

    [JsonPropertyName("link")] public Uri Link { get; init; } = default!;

    [JsonPropertyName("last_activity_date")]
    public long LastActivityDateUnix { get; init; }

    public DateTimeOffset LastActivityDate => DateTimeOffset.FromUnixTimeSeconds(LastActivityDateUnix);
}