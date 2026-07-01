using LinkTracker.AiAgent.Infrastructure.Configuration.AiAgent;
using LinkTracker.AiAgent.Infrastructure.Services;
using LinkTracker.Shared.Contracts.Bot;
using Microsoft.Extensions.Options;

namespace LinkTracker.Tests.AiAgent.Unit.Application.Services;

[Trait("Module", "AiAgent")]
[Trait("Category", "Unit")]
public sealed class LinkUpdateFilterTests
{
    private static LinkUpdateFilter CreateFilter(
        IReadOnlyList<string>? stopWords = null,
        IReadOnlyList<string>? excludedAuthors = null,
        int minLength = 0)
    {
        var options = Options.Create(new AiAgentOptions { Filtering = new FilteringOptions { StopWords = stopWords ?? [], ExcludedAuthors = excludedAuthors ?? [], MinLength = minLength } });

        return new LinkUpdateFilter(options);
    }

    private static LinkUpdate BuildUpdate(
        string description = "Normal update text that is long enough",
        string author = "regular-user")
    {
        return new LinkUpdate
        {
            Id = 1,
            Url = new Uri("https://github.com/user/repo"),
            Description = description,
            Author = author,
            TgChatIds = [123]
        };
    }

    [Fact]
    public void ShouldFilter_WhenDescriptionContainsStopWord_ReturnsTrue()
    {
        var filter = CreateFilter(["spam"]);
        var update = BuildUpdate("This is a spam message with enough length");

        Assert.True(filter.ShouldFilter(update));
    }

    [Fact]
    public void ShouldFilter_StopWordMatchIsCaseInsensitive()
    {
        var filter = CreateFilter(["SPAM"]);
        var update = BuildUpdate("This spam message should be filtered out");

        Assert.True(filter.ShouldFilter(update));
    }

    [Fact]
    public void ShouldFilter_WhenAuthorIsExcluded_ReturnsTrue()
    {
        var filter = CreateFilter(excludedAuthors: ["bot-user"]);
        var update = BuildUpdate(author: "bot-user");

        Assert.True(filter.ShouldFilter(update));
    }

    [Fact]
    public void ShouldFilter_ExcludedAuthorMatchIsCaseInsensitive()
    {
        var filter = CreateFilter(excludedAuthors: ["Bot-User"]);
        var update = BuildUpdate(author: "bot-user");

        Assert.True(filter.ShouldFilter(update));
    }

    [Fact]
    public void ShouldFilter_WhenDescriptionIsTooShort_ReturnsTrue()
    {
        var filter = CreateFilter(minLength: 20);
        var update = BuildUpdate("short");

        Assert.True(filter.ShouldFilter(update));
    }

    [Fact]
    public void ShouldFilter_WhenDescriptionEqualsMinLength_ReturnsFalse()
    {
        const string description = "exactly twenty chars";
        var filter = CreateFilter(minLength: 20);
        var update = BuildUpdate(description);

        Assert.False(filter.ShouldFilter(update));
    }

    [Fact]
    public void ShouldFilter_WhenUpdatePassesAllChecks_ReturnsFalse()
    {
        var filter = CreateFilter(
            ["spam"],
            ["bot-user"],
            10);

        var update = BuildUpdate(
            "Normal update about a new PR merged",
            "regular-user");

        Assert.False(filter.ShouldFilter(update));
    }

    [Fact]
    public void ShouldFilter_WithMultipleStopWords_FiltersOnAnyMatch()
    {
        var filter = CreateFilter(["spam", "ads", "promo"]);
        var update = BuildUpdate("Check out this promo for our product launch");

        Assert.True(filter.ShouldFilter(update));
    }
}