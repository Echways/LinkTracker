using System.Text;
using LinkTracker.Bot.Application.Commands.Helpers;
using LinkTracker.Bot.Application.Localization;
using LinkTracker.Bot.Application.Models;
using Microsoft.Extensions.Localization;

namespace LinkTracker.Bot.Application.Commands.Implementations;

public sealed class HelpCommand(
    Lazy<IEnumerable<ICommandDescriptor>> descriptors,
    IStringLocalizer<Messages> localizer) : ICommandDescriptor, ICommandHandler
{
    public string Name => "help";
    public string Description => localizer[Texts.HelpDescription];
    public bool ShowInHelp => true;
    public bool ShowInTelegramMenu => true;

    public bool CanHandle(string text)
    {
        return CommandTextMatcher.Matches(text, Name);
    }

    public Task<OutgoingMessage> ExecuteAsync(long chatId, string text, CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        sb.AppendLine(localizer[Texts.HelpHeader]);

        foreach (var d in descriptors.Value
                     .Where(d => d.ShowInHelp && d.Name != Name)
                     .OrderBy(d => d.Name))
        {
            sb.AppendLine($"/{d.Name} — {d.Description}");
        }

        return Task.FromResult(new OutgoingMessage(chatId, sb.ToString().TrimEnd()));
    }
}