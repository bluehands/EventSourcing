using System;
using System.Threading.Tasks;
using EventSourcing.Funicular.Commands.Infrastructure.Internal;

namespace EventSourcing.Funicular.Commands;

public abstract class CommandProcessor<TFailure>
    where TFailure : IFailure<TFailure>
{
    public static async Task<CommandResult<TFailure>> Process(Command command, GetCommandProcessor<TFailure> getCommandProcessor)
    {
        try
        {
            using var scopedCommandProcessor = getCommandProcessor(command.GetType());
            CommandResult<TFailure>? processingResult;
            if (scopedCommandProcessor != null)
                // ReSharper disable once AccessToDisposedClosure
                processingResult = await Task.Run(() => scopedCommandProcessor.Processor.InternalProcess(command))
                    .ConfigureAwait(false);
            else
                processingResult = CommandResult<TFailure>.Unhandled(command.Id, $"No command processor registered for command {command.GetType().Name}");
            return processingResult;
        }
        catch (OperationCanceledException)
        {
            return CommandResult<TFailure>.Cancelled(command.Id);
        }
        catch (Exception e)
        {
            return CommandResult<TFailure>.Faulted(e, command.Id, $"Process command {command} failed");
        }
    }

    protected abstract Task<CommandResult<TFailure>.Processed_> InternalProcess(Command command);
}

public abstract class CommandProcessor<T, TFailure>
    : CommandProcessor<TFailure>
    where T : Command
    where TFailure : IFailure<TFailure>
{
    protected override async Task<CommandResult<TFailure>.Processed_> InternalProcess(Command command) => await Process((T)command).ConfigureAwait(false);

    public abstract Task<CommandResult<TFailure>.Processed_> Process(T command);
}

public abstract class SynchronousCommandProcessor<T, TFailure>
    : CommandProcessor<T, TFailure>
    where T : Command
    where TFailure : IFailure<TFailure>
{
    public override Task<CommandResult<TFailure>.Processed_> Process(T command) => Task.FromResult(ProcessSync(command));

    public abstract CommandResult<TFailure>.Processed_ ProcessSync(T command);
}