using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EventSourcing.Commands.Infrastructure.Internal;
using FunicularSwitch.Generators;

namespace EventSourcing.Commands;

[UnionType(CaseOrder = CaseOrder.AsDeclared)]
public abstract partial record ProcessingResult<TError>(IReadOnlyCollection<IEventPayload> Payloads)
{
    public static implicit operator ProcessingResult<TError>(TError error) => Failed([], error);

    public record Ok_(IReadOnlyCollection<IEventPayload> Payloads, string? Message = null)
        : ProcessingResult<TError>(Payloads)
    {
        public Ok_(IEventPayload eventPayload, string? Message = null) : this([eventPayload], Message)
        {
        }

        public Ok_(string Message) : this([], Message)
        {
        }
    }

    public record Failed_(IReadOnlyCollection<IEventPayload> Payloads, TError Error)
        : ProcessingResult<TError>(Payloads)
    {
    }
}

public abstract class CommandProcessor<TError> where TError : notnull
{
    public static async Task<(CommandResult<TError> result, IReadOnlyCollection<IEventPayload> payloads)> Process(Command command, GetCommandProcessor<TError> getCommandProcessor)
    {
        try
        {
            using var scopedCommandProcessor = getCommandProcessor(command.GetType());
            if (scopedCommandProcessor != null)
                // ReSharper disable once AccessToDisposedClosure
            {
                var processingResult = await Task.Run(() => scopedCommandProcessor.Processor.InternalProcess(command))
                    .ConfigureAwait(false);

                var commandResult = CommandResult<TError>.Processed(command.Id,
                    processingResult
                        .Match(ok: ok => FunctionalResult<TError>.Ok(ok.Message ?? ""),
                            failed: failed => FunctionalResult<TError>.Failed(failed.Error))
                );
                return (commandResult, processingResult.Payloads);
            }

            return Error(CommandResult<TError>.Unhandled(command.Id, $"No command processor registered for command {command.GetType().Name}"));
        }
        catch (OperationCanceledException)
        {
            return Error(CommandResult<TError>.Cancelled(command.Id));
        }
        catch (Exception e)
        {
            return Error(CommandResult<TError>.Faulted(command.Id, $"Process command {command} failed: {e}", e));
        }

        static (CommandResult<TError> result, IReadOnlyCollection<IEventPayload> payloads) Error(
            CommandResult<TError> result) => (result, []);
    }

    protected abstract Task<ProcessingResult<TError>> InternalProcess(Command command);
}

public abstract class CommandProcessor<T, TError>
    : CommandProcessor<TError>
    where T : Command where TError : notnull
{
    protected override async Task<ProcessingResult<TError>> InternalProcess(Command command) => await Process((T)command).ConfigureAwait(false);

    public abstract Task<ProcessingResult<TError>> Process(T command);
}

public abstract class SynchronousCommandProcessor<T, TError>
    : CommandProcessor<T, TError>
    where T : Command where TError : notnull
{
    public override Task<ProcessingResult<TError>> Process(T command) => Task.FromResult(ProcessSync(command));

    public abstract ProcessingResult<TError> ProcessSync(T command);
}