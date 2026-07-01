using System.Text.Json.Serialization;

namespace LinkTracker.Scrapper.Application.Clients.StackOverflow.Contracts;

public sealed class StackOverflowAnswerResponse
{
    [JsonPropertyName("answer_id")] public long AnswerId { get; init; }

    [JsonPropertyName("body")] public string Body { get; init; } = string.Empty;

    [JsonPropertyName("link")] public Uri? Link { get; init; }

    [JsonPropertyName("owner")] public StackOverflowUserResponse? Owner { get; init; }

    [JsonPropertyName("creation_date")] public long CreationDateUnix { get; init; }

    public DateTimeOffset CreationDate => DateTimeOffset.FromUnixTimeSeconds(CreationDateUnix);
}