using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EventSourcing.Commands.Infrastructure.Internal;
using FunicularSwitch.Generators;

namespace EventSourcing.Commands;

[UnionType(CaseOrder = CaseOrder.AsDeclared)]
public abstract partial record ProcessingResult<TFailure>(IReadOnlyCollection<IEventPayload> Payloads)
{
    public static implicit operator ProcessingResult<TFailure>(TFailure failure) => Failed([], failure);

    public record Ok_(IReadOnlyCollection<IEventPayload> Payloads, string? Message = null)
        : ProcessingResult<TFailure>(Payloads)
    {
        public Ok_(IEventPayload eventPayload, string? Message = null) : this([eventPayload], Message)
        {
        }

        public Ok_(string Message) : this([], Message)
        {
        }
    }

    public record Failed_(IReadOnlyCollection<IEventPayload> Payloads, TFailure Failure)
        : ProcessingResult<TFailure>(Payloads)
    {
    }
}

public abstract class CommandProcessor<TFailure> where TFailure : notnull
{
    public static async Task<(CommandResult<TFailure> result, IReadOnlyCollection<IEventPayload> payloads)> Process(Command command, GetCommandProcessor<TFailure> getCommandProcessor)
    {
        try
        {
            using var scopedCommandProcessor = getCommandProcessor(command.GetType());
            if (scopedCommandProcessor != null)
                // ReSharper disable once AccessToDisposedClosure
            {
                var processingResult = await Task.Run(() => scopedCommandProcessor.Processor.InternalProcess(command))
                    .ConfigureAwait(false);

                var commandResult = CommandResult<TFailure>.Processed(command.Id,
                    processingResult
                        .Match(ok: ok => FunctionalResult<TFailure>.Ok(ok.Message ?? ""),
                            failed: failed => FunctionalResult<TFailure>.Failed(failed.Failure))
                );
                return (commandResult, processingResult.Payloads);
            }

            return Error(CommandResult<TFailure>.Unhandled(command.Id, $"No command processor registered for command {command.GetType().Name}"));
        }
        catch (OperationCanceledException)
        {
            return Error(CommandResult<TFailure>.Cancelled(command.Id));
        }
        catch (Exception e)
        {
            return Error(CommandResult<TFailure>.Faulted(command.Id, $"Process command {command} failed: {e}", e));
        }

        static (CommandResult<TFailure> result, IReadOnlyCollection<IEventPayload> payloads) Error(
            CommandResult<TFailure> result) => (result, []);
    }

    protected abstract Task<ProcessingResult<TFailure>> InternalProcess(Command command);
}

public abstract class CommandProcessor<T, TFailure>
    : CommandProcessor<TFailure>
    where T : Command where TFailure : notnull
{
    protected override async Task<ProcessingResult<TFailure>> InternalProcess(Command command) => await Process((T)command).ConfigureAwait(false);

    public abstract Task<ProcessingResult<TFailure>> Process(T command);
}

public abstract class SynchronousCommandProcessor<T, TFailure>
    : CommandProcessor<T, TFailure>
    where T : Command where TFailure : notnull
{
    public override Task<ProcessingResult<TFailure>> Process(T command) => Task.FromResult(ProcessSync(command));

    public abstract ProcessingResult<TFailure> ProcessSync(T command);
}