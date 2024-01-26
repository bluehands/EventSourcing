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
                await scopedCommandProcessor.Processor.InternalProcess(command).ConfigureAwait(false) :
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

    protected abstract Task<CommandResult.Processed_> InternalProcess(Command command);
}

public abstract class CommandProcessor<T> : CommandProcessor where T : Command
{
    protected override async Task<CommandResult.Processed_> InternalProcess(Command command) => await Process((T)command).ConfigureAwait(false);

    public abstract Task<CommandResult.Processed_> Process(T command);
}

public abstract class SynchronousCommandProcessor<T> : CommandProcessor<T> where T : Command
{
    public override Task<CommandResult.Processed_> Process(T command) => Task.FromResult(ProcessSync(command));

    public abstract CommandResult.Processed_ ProcessSync(T command);
}