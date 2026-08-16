using LinkTracker.AiAgent.Application.Abstractions;
using LinkTracker.AiAgent.Application.Services;
using LinkTracker.Shared.Contracts.AiAgent;
using LinkTracker.Shared.Contracts.Bot;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace LinkTracker.Tests.AiAgent.Unit.Application.Services;

[Trait("Module", "AiAgent")]
[Trait("Category", "Unit")]
public sealed class LinkUpdateProcessingServiceTests
{
    private readonly ILinkUpdateFilter _filter = Substitute.For<ILinkUpdateFilter>();
    private readonly IGroupingBuffer _groupingBuffer = Substitute.For<IGroupingBuffer>();
    private readonly ILinkUpdatePrioritizer _prioritizer = Substitute.For<ILinkUpdatePrioritizer>();
    private readonly IProcessedUpdatePublisher _publisher = Substitute.For<IProcessedUpdatePublisher>();
    private readonly ILinkUpdateSummarizer _summarizer = Substitute.For<ILinkUpdateSummarizer>();
    private readonly IMessageAck _ack = Substitute.For<IMessageAck>();

    private LinkUpdateProcessingService CreateService()
    {
        return new LinkUpdateProcessingService(_filter, _summarizer, _prioritizer, _groupingBuffer, _publisher,
            NullLogger<LinkUpdateProcessingService>.Instance);
    }

    private static LinkUpdate BuildUpdate(string description = "some long description text")
    {
        return new LinkUpdate
        {
            Id = 1,
            Url = new Uri("https://github.com/user/repo"),
            Description = description,
            Author = "regular-user",
            TgChatIds = [42]
        };
    }

    [Fact]
    public async Task ProcessAsync_WhenFilterRejectsUpdate_DoesNotAddToBuffer()
    {
        _filter.ShouldFilter(Arg.Any<LinkUpdate>()).Returns(true);

        await CreateService().ProcessAsync(BuildUpdate(), _ack, CancellationToken.None);

        _groupingBuffer.DidNotReceive().Add(Arg.Any<long>(), Arg.Any<ProcessedLinkUpdate>(), Arg.Any<IMessageAck>());
    }

    [Fact]
    public async Task ProcessAsync_WhenFilterRejectsUpdate_DoesNotCallSummarizer()
    {
        _filter.ShouldFilter(Arg.Any<LinkUpdate>()).Returns(true);

        await CreateService().ProcessAsync(BuildUpdate(), _ack, CancellationToken.None);

        await _summarizer.DidNotReceive().SummarizeAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_WhenFilterRejectsUpdate_DoesNotCallPrioritizer()
    {
        _filter.ShouldFilter(Arg.Any<LinkUpdate>()).Returns(true);

        await CreateService().ProcessAsync(BuildUpdate(), _ack, CancellationToken.None);

        _prioritizer.DidNotReceive().Prioritize(Arg.Any<string>());
    }

    [Fact]
    public async Task ProcessAsync_WhenUpdatePasses_CallsSummarizerWithOriginalDescription()
    {
        var update = BuildUpdate("original description");
        _filter.ShouldFilter(update).Returns(false);
        _summarizer.SummarizeAsync("original description", Arg.Any<CancellationToken>())
            .Returns("summarized");

        await CreateService().ProcessAsync(update, _ack, CancellationToken.None);

        await _summarizer.Received(1).SummarizeAsync(
            "original description",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_WhenUpdatePasses_CallsPrioritizerWithSummarizedDescription()
    {
        _filter.ShouldFilter(Arg.Any<LinkUpdate>()).Returns(false);
        _summarizer.SummarizeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("summarized text");
        _prioritizer.Prioritize(Arg.Any<string>()).Returns(LinkUpdatePriority.Medium);

        await CreateService().ProcessAsync(BuildUpdate(), _ack, CancellationToken.None);

        _prioritizer.Received(1).Prioritize("summarized text");
    }

    [Fact]
    public async Task ProcessAsync_WhenUpdatePasses_AddsToBufferForEachChatId()
    {
        var update = new LinkUpdate
        {
            Id = 1,
            Url = new Uri("https://github.com/user/repo"),
            Description = "some description",
            Author = "regular-user",
            TgChatIds = [42, 99]
        };

        _filter.ShouldFilter(update).Returns(false);
        _summarizer.SummarizeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("summarized");
        _prioritizer.Prioritize(Arg.Any<string>()).Returns(LinkUpdatePriority.High);

        await CreateService().ProcessAsync(update, _ack, CancellationToken.None);

        _groupingBuffer.Received(1).Add(42L, Arg.Any<ProcessedLinkUpdate>(), _ack);
        _groupingBuffer.Received(1).Add(99L, Arg.Any<ProcessedLinkUpdate>(), _ack);
    }

    [Fact]
    public async Task ProcessAsync_WhenUpdatePasses_BufferedUpdateHasCorrectPriority()
    {
        _filter.ShouldFilter(Arg.Any<LinkUpdate>()).Returns(false);
        _summarizer.SummarizeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("text");
        _prioritizer.Prioritize(Arg.Any<string>()).Returns(LinkUpdatePriority.High);

        await CreateService().ProcessAsync(BuildUpdate(), _ack, CancellationToken.None);

        _groupingBuffer.Received(1).Add(
            Arg.Any<long>(),
            Arg.Is<ProcessedLinkUpdate>(p => p.Priority == LinkUpdatePriority.High),
            Arg.Any<IMessageAck>());
    }

    [Fact]
    public async Task ProcessAsync_WhenUpdatePasses_BufferedUpdateHasSummarizedDescription()
    {
        _filter.ShouldFilter(Arg.Any<LinkUpdate>()).Returns(false);
        _summarizer.SummarizeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("summarized text");
        _prioritizer.Prioritize(Arg.Any<string>()).Returns(LinkUpdatePriority.Medium);

        await CreateService().ProcessAsync(BuildUpdate(), _ack, CancellationToken.None);

        _groupingBuffer.Received(1).Add(
            Arg.Any<long>(),
            Arg.Is<ProcessedLinkUpdate>(p => p.Description == "summarized text"),
            Arg.Any<IMessageAck>());
    }

    [Fact]
    public async Task ProcessAsync_WhenSystemReport_PublishesDirectlyWithoutFilterSummarizationAndGrouping()
    {
        var update = new LinkUpdate
        {
            Id = 0,
            Url = new Uri("https://github.com/user/repo"),
            Description = "Не удалось проверить часть ссылок: spam",
            TgChatIds = [42],
            Kind = LinkUpdateKind.SystemReport
        };

        // Стоп-слово в тексте отчета не должно приводить к его отбрасыванию.
        _filter.ShouldFilter(Arg.Any<LinkUpdate>()).Returns(true);

        await CreateService().ProcessAsync(update, _ack, CancellationToken.None);

        await _publisher.Received(1).PublishAsync(
            Arg.Is<ProcessedLinkUpdate>(p =>
                p.Kind == LinkUpdateKind.SystemReport &&
                p.Description == "Не удалось проверить часть ссылок: spam" &&
                p.TgChatIds.Count == 1 &&
                p.TgChatIds[0] == 42L),
            Arg.Any<CancellationToken>());

        _filter.DidNotReceive().ShouldFilter(Arg.Any<LinkUpdate>());
        await _summarizer.DidNotReceive().SummarizeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        _groupingBuffer.DidNotReceive().Add(Arg.Any<long>(), Arg.Any<ProcessedLinkUpdate>(), Arg.Any<IMessageAck>());
    }

    [Fact]
    public async Task ProcessAsync_WhenSystemReportPublishingFails_PropagatesException()
    {
        var update = new LinkUpdate
        {
            Id = 0,
            Url = new Uri("https://github.com/user/repo"),
            Description = "Не удалось проверить часть ссылок",
            TgChatIds = [42],
            Kind = LinkUpdateKind.SystemReport
        };

        _publisher
            .PublishAsync(Arg.Any<ProcessedLinkUpdate>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("Kafka down")));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateService().ProcessAsync(update, _ack, CancellationToken.None));
    }
}
