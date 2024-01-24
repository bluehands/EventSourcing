using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EventSourcing.Commands.Internal;
using FunicularSwitch.Generators;

namespace EventSourcing.Commands;

[UnionType(CaseOrder = CaseOrder.AsDeclared)]
public abstract partial record FunctionalResult()
{
    public sealed record Ok(string Message) : FunctionalResult();
    public sealed record Failed(Failure Failure) : FunctionalResult();

    public string Message => this.Match(ok => ok.Message, failed => failed.Message);
}

[UnionType]
public abstract partial class ProcessingResult(CommandId commandId, string? message)
{
    public CommandId CommandId { get; } = commandId;
    public string? Message { get; } = message;

    public sealed class Processed_ : ProcessingResult
    {
        public IReadOnlyCollection<EventPayload> ResultEvents { get; }

        public FunctionalResult FunctionalResult { get; }

        public Processed_(EventPayload resultEvent, CommandId commandId, FunctionalResult functionalResult) : this(new[] { resultEvent }, commandId, functionalResult)
        {
        }

        public Processed_(IReadOnlyCollection<EventPayload> resultEvents, CommandId commandId, FunctionalResult functionalResult) : base(commandId, functionalResult.Message)
        {
            FunctionalResult = functionalResult;
            ResultEvents = resultEvents;
        }

        public override string ToString() => $"Command {CommandId} processed with result {FunctionalResult}, Produced events count: {ResultEvents.Count}";
    }

    public sealed class Faulted_(Exception exception, CommandId commandId, string message) : ProcessingResult(commandId, message)
    {
        public Exception Exception { get; } = exception;

        public override string ToString() => $"Command {CommandId} faulted: {Message} - {Exception}";
    }

    public sealed class Unhandled_(CommandId commandId, string message) : ProcessingResult(commandId, message)
    {
        public new string Message { get; } = message;

        public override string ToString() => $"Command {CommandId} unhandled: {Message}";
    }

    public sealed class Cancelled_(CommandId commandId) : ProcessingResult(commandId, $"Command {commandId} cancelled")
    {
        public override string ToString() => $"Command {CommandId} cancelled";
    }
}

public record CommandId(Guid Id)
{
    public static CommandId NewCommandId() => new(Guid.NewGuid());
    public override string ToString() => Id.ToString("N");
}

public delegate ScopedCommandProcessor? GetCommandProcessor(Type commandType);

public abstract record Command
{
    public CommandId Id { get; } = CommandId.NewCommandId();

    public override string ToString() => $"{GetType().Name} ({Id.Id})";
}

public abstract class CommandProcessor
{
    public static async Task<ProcessingResult> Process(Command command, GetCommandProcessor getCommandProcessor)
    {
        try
        {
            using var scopedCommandProcessor = getCommandProcessor(command.GetType());
            var processingResult = scopedCommandProcessor != null ?
                await scopedCommandProcessor.Processor.Process(command).ConfigureAwait(false) :
                ProcessingResult.Unhandled(command.Id, $"No command processor registered for command {command.GetType().Name}");
            return processingResult;
        }
        catch (OperationCanceledException)
        {
            return ProcessingResult.Cancelled(command.Id);
        }
        catch (Exception e)
        {
            return ProcessingResult.Faulted(e, command.Id, $"Process command {command} failed");
        }
    }

    protected abstract Task<ProcessingResult.Processed_> Process(Command command);
}

public abstract class CommandProcessor<T> : CommandProcessor where T : Command
{
    protected override async Task<ProcessingResult.Processed_> Process(Command command) => await InternalProcess((T)command).ConfigureAwait(false);

    public abstract Task<ProcessingResult.Processed_> InternalProcess(T command);
}

public abstract class SynchronousCommandProcessor<T> : CommandProcessor<T> where T : Command
{
    public override Task<ProcessingResult.Processed_> InternalProcess(T command) =>
        Task.FromResult(InternalProcessSync(command));

    public abstract ProcessingResult.Processed_ InternalProcessSync(T command);
}