using LinkTracker.Scrapper.Application.Clients.StackOverflow;
using LinkTracker.Scrapper.Application.Clients.StackOverflow.Contracts;
using LinkTracker.Scrapper.Application.Models.Updates;
using LinkTracker.Scrapper.Application.Services.Helpers;
using LinkTracker.Scrapper.Storage.Abstractions.Models;
using Microsoft.Extensions.Logging;

namespace LinkTracker.Scrapper.Application.Services.Updates.Clients;

public sealed class StackOverflowLinkUpdateHandler(
    IStackOverflowClient stackOverflowClient,
    ILogger<StackOverflowLinkUpdateHandler> logger) : LinkUpdateHandlerBase(logger)
{
    public override bool CanHandle(Uri url)
    {
        return TryParseQuestionId(url, out _);
    }

    protected override async Task<LinkCheckResult> InitializeStateAsync(
        TrackedLinkSubscription subscription,
        CancellationToken ct)
    {
        TryParseQuestionId(subscription.Url, out var questionId);

        var questionResponse = await stackOverflowClient.GetQuestionAsync(questionId, ct);

        return questionResponse is null
            ? LinkUpdateResultBuilder.NoChanges()
            : LinkUpdateResultBuilder.InitialState(questionResponse.LastActivityDate);
    }

    protected override async Task<IReadOnlyList<LinkEvent>> GetNewEventsAsync(
        TrackedLinkSubscription subscription,
        DateTimeOffset lastSeenAt,
        string? lastEventKey,
        CancellationToken ct)
    {
        TryParseQuestionId(subscription.Url, out var questionId);

        var answersTask = stackOverflowClient.GetAnswersAsync(questionId, ct);
        var commentsTask = stackOverflowClient.GetCommentsAsync(questionId, ct);

        await Task.WhenAll(answersTask, commentsTask);

        var answers = await answersTask;
        var comments = await commentsTask;

        var hasNewAnswers = answers.Any(x =>
            IsAfterCursor(x.CreationDate, $"answer:{x.AnswerId}", lastSeenAt, lastEventKey));

        var hasNewComments = comments.Any(x =>
            IsAfterCursor(x.CreationDate, $"comment:{x.CommentId}", lastSeenAt, lastEventKey));

        if (!hasNewAnswers && !hasNewComments)
        {
            return [];
        }

        var questionResponse = await stackOverflowClient.GetQuestionAsync(questionId, ct);
        var title = questionResponse?.Title ?? subscription.Url.AbsoluteUri;

        var events = new List<LinkEvent>();

        events.AddRange(
            answers
                .Select(x => MapAnswerToEvent(x, title, subscription.Url))
                .Where(x => IsAfterCursor(x, lastSeenAt, lastEventKey)));

        events.AddRange(
            comments
                .Select(x => MapCommentToEvent(x, title, subscription.Url))
                .Where(x => IsAfterCursor(x, lastSeenAt, lastEventKey)));

        return events;
    }

    private static bool TryParseQuestionId(Uri url, out long questionId)
    {
        questionId = default;

        if (!UriParsingHelper.IsHost(url, "stackoverflow.com"))
        {
            return false;
        }

        var segments = UriParsingHelper.GetPathSegments(url);
        if (segments.Length < 2)
        {
            return false;
        }

        if (!string.Equals(segments[0], "questions", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return long.TryParse(segments[1], out questionId) && questionId > 0;
    }

    private static LinkEvent MapAnswerToEvent(
        StackOverflowAnswerResponse answer,
        string title,
        Uri questionUrl)
    {
        return new LinkEvent
        {
            SourceKind = LinkSourceKind.StackOverflow,
            EventKind = LinkEventKind.Answer,
            Title = title,
            UserName = answer.Owner?.DisplayName ?? string.Empty,
            CreatedAt = answer.CreationDate,
            EventKey = $"answer:{answer.AnswerId}",
            Body = answer.Body,
            ResourceUrl = answer.Link ?? questionUrl
        };
    }

    private static LinkEvent MapCommentToEvent(
        StackOverflowCommentResponse comment,
        string title,
        Uri questionUrl)
    {
        return new LinkEvent
        {
            SourceKind = LinkSourceKind.StackOverflow,
            EventKind = LinkEventKind.Comment,
            Title = title,
            UserName = comment.Owner?.DisplayName ?? string.Empty,
            CreatedAt = comment.CreationDate,
            EventKey = $"comment:{comment.CommentId}",
            Body = comment.Body,
            ResourceUrl = comment.Link ?? questionUrl
        };
    }
}