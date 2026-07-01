using LinkTracker.AiAgent.Infrastructure.Services;
using LinkTracker.Shared.Contracts.AiAgent;

namespace LinkTracker.Tests.AiAgent.Unit.Application.Services;

[Trait("Module", "AiAgent")]
[Trait("Category", "Unit")]
public sealed class LinkUpdateGrouperTests
{
    private static WindowLinkUpdateGrouper CreateGrouper()
    {
        return new WindowLinkUpdateGrouper();
    }

    private static ProcessedLinkUpdate BuildUpdate(
        string description = "Some update text",
        LinkUpdatePriority priority = LinkUpdatePriority.Medium)
    {
        return new ProcessedLinkUpdate
        {
            Id = 1,
            Url = new Uri("https://github.com/user/repo"),
            Description = description,
            TgChatIds = [42],
            Priority = priority
        };
    }

    [Fact]
    public void Group_WhenMultipleUpdates_MergesIntoOne()
    {
        var grouper = CreateGrouper();
        var updates = new List<ProcessedLinkUpdate> { BuildUpdate("First update", LinkUpdatePriority.Low), BuildUpdate("Second update", LinkUpdatePriority.High), BuildUpdate("Third update", LinkUpdatePriority.Medium) };

        var result = grouper.Group(updates);

        Assert.Single(result);
    }

    [Fact]
    public void Group_WhenMultipleUpdates_DescriptionIsNumberedList()
    {
        var grouper = CreateGrouper();
        var updates = new List<ProcessedLinkUpdate> { BuildUpdate("First update"), BuildUpdate("Second update") };

        var result = grouper.Group(updates);

        Assert.Contains("1. [https://github.com/user/repo] First update", result[0].Description);
        Assert.Contains("2. [https://github.com/user/repo] Second update", result[0].Description);
    }

    [Fact]
    public void Group_WhenMultipleUpdates_PriorityIsMaximum()
    {
        var grouper = CreateGrouper();
        var updates = new List<ProcessedLinkUpdate> { BuildUpdate(priority: LinkUpdatePriority.Low), BuildUpdate(priority: LinkUpdatePriority.High), BuildUpdate(priority: LinkUpdatePriority.Medium) };

        var result = grouper.Group(updates);

        Assert.Equal(LinkUpdatePriority.High, result[0].Priority);
    }

    [Fact]
    public void Group_WhenSingleUpdate_ReturnsSameWithoutModification()
    {
        var grouper = CreateGrouper();
        var update = BuildUpdate("Only one update", LinkUpdatePriority.Medium);

        var result = grouper.Group([update]);

        Assert.Single(result);
        Assert.Equal("Only one update", result[0].Description);
        Assert.Equal(LinkUpdatePriority.Medium, result[0].Priority);
    }

    [Fact]
    public void Group_WhenSingleUpdate_DoesNotApplyNumbering()
    {
        var grouper = CreateGrouper();
        var update = BuildUpdate("Solo update");

        var result = grouper.Group([update]);

        Assert.DoesNotContain("1.", result[0].Description);
    }
}