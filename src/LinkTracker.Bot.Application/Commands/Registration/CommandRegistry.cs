namespace LinkTracker.Bot.Application.Commands.Registration;

public sealed class CommandRegistry(IEnumerable<ICommandDescriptor> commands)
{
    private readonly IReadOnlyList<ICommandDescriptor> _commands = commands.ToList();

    public IReadOnlyList<TelegramMenuCommand> GetTelegramCommands()
    {
        return _commands
            .Where(d => d.ShowInTelegramMenu)
            .Select(c => new TelegramMenuCommand(c.Name, c.Description))
            .ToList();
    }
}