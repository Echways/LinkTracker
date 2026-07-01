using LinkTracker.Scrapper.Application.Clients.StackOverflow;
using LinkTracker.Scrapper.Application.Clients.StackOverflow.Contracts;
using LinkTracker.Scrapper.Application.Models.Updates;
using LinkTracker.Scrapper.Application.Services.Updates.Clients;
using LinkTracker.Scrapper.Storage.Abstractions.Models;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace LinkTracker.Tests.Scrapper.Unit.Application.Services.Updates.Clients;

[Trait("Module", "Scrapper")]
[Trait("Category", "Unit")]
public sealed class StackOverflowLinkUpdateHandlerTests
{
    public static TheoryData<string> SingleEventKinds =>
        new() { nameof(LinkEventKind.Answer), nameof(LinkEventKind.Comment) };

    [Fact]
    public async Task CheckAsync_WhenLastUpdatedAtIsNull_ReturnsInitialStateWithoutEvents()
    {
        var stackOverflowClient = Substitute.For<IStackOverflowClient>();
        var logger = Substitute.For<ILogger<StackOverflowLinkUpdateHandler>>();

        var lastActivityDate = new DateTimeOffset(2025, 3, 10, 12, 0, 0, TimeSpan.Zero);

        stackOverflowClient.GetQuestionAsync(123, Arg.Any<CancellationToken>())
            .Returns(new StackOverflowQuestionResponse { QuestionId = 123, Title = "How to test this?", Link = new Uri("https://stackoverflow.com/questions/123/how-to-test-this"), LastActivityDateUnix = lastActivityDate.ToUnixTimeSeconds() });

        var subscription = new TrackedLinkSubscription { Id = 1, Url = new Uri("https://stackoverflow.com/questions/123/how-to-test-this"), TgChatIds = [1001L], LastUpdatedAt = null };

        var sut = new StackOverflowLinkUpdateHandler(stackOverflowClient, logger);

        var result = await sut.CheckAsync(subscription);

        Assert.False(result.HasChanges);
        Assert.Empty(result.Events);
        Assert.Equal(lastActivityDate, result.NewLastUpdatedAt);

        await stackOverflowClient.Received(1)
            .GetQuestionAsync(123, Arg.Any<CancellationToken>());

        await stackOverflowClient.DidNotReceive()
            .GetAnswersAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());

        await stackOverflowClient.DidNotReceive()
            .GetCommentsAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckAsync_WhenNoNewAnswersOrComments_ReturnsNoChangesAndKeepsLastUpdatedAt()
    {
        var stackOverflowClient = Substitute.For<IStackOverflowClient>();
        var logger = Substitute.For<ILogger<StackOverflowLinkUpdateHandler>>();

        var lastSeenAt = new DateTimeOffset(2025, 3, 9, 12, 0, 0, TimeSpan.Zero);

        stackOverflowClient.GetAnswersAsync(123, Arg.Any<CancellationToken>())
            .Returns(
            [
                new StackOverflowAnswerResponse
                {
                    AnswerId = 777,
                    Body = "<p>Old answer</p>",
                    Link = new Uri("https://stackoverflow.com/a/777"),
                    Owner = new StackOverflowUserResponse { DisplayName = "alice" },
                    CreationDateUnix = lastSeenAt.AddMinutes(-1).ToUnixTimeSeconds()
                }
            ]);

        stackOverflowClient.GetCommentsAsync(123, Arg.Any<CancellationToken>())
            .Returns(
            [
                new StackOverflowCommentResponse
                {
                    CommentId = 888,
                    Body = "<p>Old comment</p>",
                    Link = new Uri("https://stackoverflow.com/questions/123/how-to-test-this#comment888_123"),
                    Owner = new StackOverflowUserResponse { DisplayName = "bob" },
                    CreationDateUnix = lastSeenAt.AddMinutes(-1).ToUnixTimeSeconds()
                }
            ]);

        var subscription = new TrackedLinkSubscription { Id = 1, Url = new Uri("https://stackoverflow.com/questions/123/how-to-test-this"), TgChatIds = [1001L], LastUpdatedAt = lastSeenAt };

        var sut = new StackOverflowLinkUpdateHandler(stackOverflowClient, logger);

        var result = await sut.CheckAsync(subscription);

        Assert.False(result.HasChanges);
        Assert.Empty(result.Events);
        Assert.Equal(lastSeenAt, result.NewLastUpdatedAt);

        await stackOverflowClient.DidNotReceive()
            .GetQuestionAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [MemberData(nameof(SingleEventKinds))]
    public async Task CheckAsync_WhenSingleNewEventExists_ReturnsMappedEvent(string eventKindName)
    {
        var stackOverflowClient = Substitute.For<IStackOverflowClient>();
        var logger = Substitute.For<ILogger<StackOverflowLinkUpdateHandler>>();

        var lastSeenAt = new DateTimeOffset(2025, 3, 9, 12, 0, 0, TimeSpan.Zero);
        var createdAt = new DateTimeOffset(2025, 3, 10, 10, 0, 0, TimeSpan.Zero);

        stackOverflowClient.GetQuestionAsync(123, Arg.Any<CancellationToken>())
            .Returns(new StackOverflowQuestionResponse { QuestionId = 123, Title = "How to test this?", Link = new Uri("https://stackoverflow.com/questions/123/how-to-test-this"), LastActivityDateUnix = createdAt.ToUnixTimeSeconds() });

        var expectedKind = Enum.Parse<LinkEventKind>(eventKindName);

        if (expectedKind == LinkEventKind.Answer)
        {
            stackOverflowClient.GetAnswersAsync(123, Arg.Any<CancellationToken>())
                .Returns(
                [
                    new StackOverflowAnswerResponse
                    {
                        AnswerId = 777,
                        Body = "<p>New answer body</p>",
                        Link = new Uri("https://stackoverflow.com/a/777"),
                        Owner = new StackOverflowUserResponse { DisplayName = "alice" },
                        CreationDateUnix = createdAt.ToUnixTimeSeconds()
                    }
                ]);

            stackOverflowClient.GetCommentsAsync(123, Arg.Any<CancellationToken>())
                .Returns(Array.Empty<StackOverflowCommentResponse>());
        }
        else
        {
            stackOverflowClient.GetAnswersAsync(123, Arg.Any<CancellationToken>())
                .Returns(Array.Empty<StackOverflowAnswerResponse>());

            stackOverflowClient.GetCommentsAsync(123, Arg.Any<CancellationToken>())
                .Returns(
                [
                    new StackOverflowCommentResponse
                    {
                        CommentId = 888,
                        Body = "<p>New comment body</p>",
                        Link = new Uri("https://stackoverflow.com/questions/123/how-to-test-this#comment888_123"),
                        Owner = new StackOverflowUserResponse { DisplayName = "bob" },
                        CreationDateUnix = createdAt.ToUnixTimeSeconds()
                    }
                ]);
        }

        var subscription = new TrackedLinkSubscription { Id = 1, Url = new Uri("https://stackoverflow.com/questions/123/how-to-test-this"), TgChatIds = [1001L], LastUpdatedAt = lastSeenAt };

        var sut = new StackOverflowLinkUpdateHandler(stackOverflowClient, logger);

        var result = await sut.CheckAsync(subscription);

        Assert.True(result.HasChanges);

        var linkEvent = Assert.Single(result.Events);
        Assert.Equal(LinkSourceKind.StackOverflow, linkEvent.SourceKind);
        Assert.Equal(expectedKind, linkEvent.EventKind);
        Assert.Equal("How to test this?", linkEvent.Title);
        Assert.Equal(createdAt, linkEvent.CreatedAt);
        Assert.Equal(createdAt, result.NewLastUpdatedAt);

        if (expectedKind == LinkEventKind.Answer)
        {
            Assert.Equal("alice", linkEvent.UserName);
            Assert.Equal("<p>New answer body</p>", linkEvent.Body);
            Assert.Equal(new Uri("https://stackoverflow.com/a/777"), linkEvent.ResourceUrl);
        }
        else
        {
            Assert.Equal("bob", linkEvent.UserName);
            Assert.Equal("<p>New comment body</p>", linkEvent.Body);
            Assert.Equal(new Uri("https://stackoverflow.com/questions/123/how-to-test-this#comment888_123"), linkEvent.ResourceUrl);
        }
    }

    [Fact]
    public async Task CheckAsync_WhenAnswerAndCommentExist_ReturnsOrderedEvents()
    {
        var stackOverflowClient = Substitute.For<IStackOverflowClient>();
        var logger = Substitute.For<ILogger<StackOverflowLinkUpdateHandler>>();

        var lastSeenAt = new DateTimeOffset(2025, 3, 9, 12, 0, 0, TimeSpan.Zero);

        var answerCreatedAt = new DateTimeOffset(2025, 3, 10, 10, 0, 0, TimeSpan.Zero);
        var commentCreatedAt = new DateTimeOffset(2025, 3, 10, 11, 0, 0, TimeSpan.Zero);

        stackOverflowClient.GetQuestionAsync(123, Arg.Any<CancellationToken>())
            .Returns(new StackOverflowQuestionResponse { QuestionId = 123, Title = "How to test this?", Link = new Uri("https://stackoverflow.com/questions/123/how-to-test-this"), LastActivityDateUnix = commentCreatedAt.ToUnixTimeSeconds() });

        stackOverflowClient.GetAnswersAsync(123, Arg.Any<CancellationToken>())
            .Returns(
            [
                new StackOverflowAnswerResponse
                {
                    AnswerId = 777,
                    Body = "<p>Answer body</p>",
                    Link = new Uri("https://stackoverflow.com/a/777"),
                    Owner = new StackOverflowUserResponse { DisplayName = "alice" },
                    CreationDateUnix = answerCreatedAt.ToUnixTimeSeconds()
                }
            ]);

        stackOverflowClient.GetCommentsAsync(123, Arg.Any<CancellationToken>())
            .Returns(
            [
                new StackOverflowCommentResponse
                {
                    CommentId = 888,
                    Body = "<p>Comment body</p>",
                    Link = new Uri("https://stackoverflow.com/questions/123/how-to-test-this#comment888_123"),
                    Owner = new StackOverflowUserResponse { DisplayName = "bob" },
                    CreationDateUnix = commentCreatedAt.ToUnixTimeSeconds()
                }
            ]);

        var subscription = new TrackedLinkSubscription { Id = 1, Url = new Uri("https://stackoverflow.com/questions/123/how-to-test-this"), TgChatIds = [1001L], LastUpdatedAt = lastSeenAt };

        var sut = new StackOverflowLinkUpdateHandler(stackOverflowClient, logger);

        var result = await sut.CheckAsync(subscription);

        Assert.True(result.HasChanges);
        Assert.Equal(2, result.Events.Count);

        Assert.Equal(LinkEventKind.Answer, result.Events[0].EventKind);
        Assert.Equal("alice", result.Events[0].UserName);

        Assert.Equal(LinkEventKind.Comment, result.Events[1].EventKind);
        Assert.Equal("bob", result.Events[1].UserName);

        Assert.Equal(commentCreatedAt, result.NewLastUpdatedAt);
    }
}