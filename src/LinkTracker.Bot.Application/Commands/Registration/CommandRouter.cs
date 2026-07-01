using System.Diagnostics;
using LinkTracker.Bot.Application.Models;
using LinkTracker.Bot.Application.Telemetry.Abstractions;

namespace LinkTracker.Bot.Application.Commands.Registration;

public sealed class CommandRouter(
    IEnumerable<ICommandHandler> commands,
    IBotMetrics metrics)
{
    private readonly IReadOnlyList<ICommandHandler> _commands = commands.ToList();

    public async Task<OutgoingMessage> RouteAsync(
        long chatId,
        string text,
        CancellationToken ct = default)
    {
        var cmd = _commands.FirstOrDefault(c => c.CanHandle(text));

        if (cmd is null)
        {
            return new OutgoingMessage(chatId, "Не понял. Напиши /help");
        }

        var commandName =
            cmd is ICommandDescriptor descriptor
                ? descriptor.Name
                : cmd.GetType().Name;

        metrics.IncrementCommand(commandName);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            return await cmd.ExecuteAsync(chatId, text, ct);
        }
        catch
        {
            metrics.IncrementError(
                "bot_command",
                commandName,
                "exception");
            throw;
        }
        finally
        {
            stopwatch.Stop();

            metrics.ObserveCommandDuration(
                "bot_command",
                commandName,
                stopwatch.Elapsed.TotalMilliseconds);
        }
    }
}