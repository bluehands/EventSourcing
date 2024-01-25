using System;
using System.Threading.Tasks;
using EventSourcing.Funicular.Commands.Infrastructure.Internal;

namespace EventSourcing.Funicular.Commands;

public abstract class CommandProcessor
{
    public static async Task<CommandResult> Process(Command command, GetCommandProcessor getCommandProcessor)
    {
        try
        {
            using var scopedCommandProcessor = getCommandProcessor(command.GetType());
            var processingResult = scopedCommandProcessor != null ?
                await scopedCommandProcessor.Processor.Process(command).ConfigureAwait(false) :
                CommandResult.Unhandled(command.Id, $"No command processor registered for command {command.GetType().Name}");
            return processingResult;
        }
        catch (OperationCanceledException)
        {
            return CommandResult.Cancelled(command.Id);
        }
        catch (Exception e)
        {
            return CommandResult.Faulted(e, command.Id, $"Process command {command} failed");
        }
    }

    protected abstract Task<CommandResult.Processed_> Process(Command command);
}

public abstract class CommandProcessor<T> : CommandProcessor where T : Command
{
    protected override async Task<CommandResult.Processed_> Process(Command command) => await InternalProcess((T)command).ConfigureAwait(false);

    public abstract Task<CommandResult.Processed_> InternalProcess(T command);
}

public abstract class SynchronousCommandProcessor<T> : CommandProcessor<T> where T : Command
{
    public override Task<CommandResult.Processed_> InternalProcess(T command) =>
        Task.FromResult(InternalProcessSync(command));

    public abstract CommandResult.Processed_ InternalProcessSync(T command);
}