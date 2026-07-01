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
    private readonly ILinkUpdateSummarizer _summarizer = Substitute.For<ILinkUpdateSummarizer>();

    private LinkUpdateProcessingService CreateService()
    {
        return new LinkUpdateProcessingService(_filter, _summarizer, _prioritizer, _groupingBuffer,
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

        await CreateService().ProcessAsync(BuildUpdate(), CancellationToken.None);

        _groupingBuffer.DidNotReceive().Add(Arg.Any<long>(), Arg.Any<ProcessedLinkUpdate>());
    }

    [Fact]
    public async Task ProcessAsync_WhenFilterRejectsUpdate_DoesNotCallSummarizer()
    {
        _filter.ShouldFilter(Arg.Any<LinkUpdate>()).Returns(true);

        await CreateService().ProcessAsync(BuildUpdate(), CancellationToken.None);

        await _summarizer.DidNotReceive().SummarizeAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_WhenFilterRejectsUpdate_DoesNotCallPrioritizer()
    {
        _filter.ShouldFilter(Arg.Any<LinkUpdate>()).Returns(true);

        await CreateService().ProcessAsync(BuildUpdate(), CancellationToken.None);

        _prioritizer.DidNotReceive().Prioritize(Arg.Any<string>());
    }

    [Fact]
    public async Task ProcessAsync_WhenUpdatePasses_CallsSummarizerWithOriginalDescription()
    {
        var update = BuildUpdate("original description");
        _filter.ShouldFilter(update).Returns(false);
        _summarizer.SummarizeAsync("original description", Arg.Any<CancellationToken>())
            .Returns("summarized");

        await CreateService().ProcessAsync(update, CancellationToken.None);

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

        await CreateService().ProcessAsync(BuildUpdate(), CancellationToken.None);

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

        await CreateService().ProcessAsync(update, CancellationToken.None);

        _groupingBuffer.Received(1).Add(42L, Arg.Any<ProcessedLinkUpdate>());
        _groupingBuffer.Received(1).Add(99L, Arg.Any<ProcessedLinkUpdate>());
    }

    [Fact]
    public async Task ProcessAsync_WhenUpdatePasses_BufferedUpdateHasCorrectPriority()
    {
        _filter.ShouldFilter(Arg.Any<LinkUpdate>()).Returns(false);
        _summarizer.SummarizeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("text");
        _prioritizer.Prioritize(Arg.Any<string>()).Returns(LinkUpdatePriority.High);

        await CreateService().ProcessAsync(BuildUpdate(), CancellationToken.None);

        _groupingBuffer.Received(1).Add(
            Arg.Any<long>(),
            Arg.Is<ProcessedLinkUpdate>(p => p.Priority == LinkUpdatePriority.High));
    }

    [Fact]
    public async Task ProcessAsync_WhenUpdatePasses_BufferedUpdateHasSummarizedDescription()
    {
        _filter.ShouldFilter(Arg.Any<LinkUpdate>()).Returns(false);
        _summarizer.SummarizeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("summarized text");
        _prioritizer.Prioritize(Arg.Any<string>()).Returns(LinkUpdatePriority.Medium);

        await CreateService().ProcessAsync(BuildUpdate(), CancellationToken.None);

        _groupingBuffer.Received(1).Add(
            Arg.Any<long>(),
            Arg.Is<ProcessedLinkUpdate>(p => p.Description == "summarized text"));
    }
}