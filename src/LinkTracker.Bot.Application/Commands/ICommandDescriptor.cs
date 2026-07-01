namespace LinkTracker.Bot.Application.Commands;

public interface ICommandDescriptor
{
    string Name { get; }
    string Description { get; }
    bool ShowInHelp { get; }
    bool ShowInTelegramMenu { get; }
}