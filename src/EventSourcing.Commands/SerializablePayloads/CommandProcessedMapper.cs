using System;

namespace EventSourcing.Funicular.Commands.SerializablePayloads;

public class CommandProcessedMapper<TFailure, TFailurePayload>
    : EventPayloadMapper<CommandProcessed<TFailure>, CommandProcessedPayload<TFailurePayload>>
    where TFailurePayload : class, IFailurePayload<TFailure, TFailurePayload> where TFailure : notnull
{
    protected override CommandProcessed<TFailure> MapFromSerializablePayload(CommandProcessedPayload<TFailurePayload> serialized, StreamId streamId)
    {
        var commandId = new CommandId(serialized.CommandId);
        return new(serialized.CommandResult switch
        {
            CommandResultUnionCases.Processed => CommandResult<TFailure>.Processed(commandId,
                serialized.FunctionalResult switch
                {
                    FunctionalResultUnionCases.Ok => FunctionalResult<TFailure>.Ok(serialized.ResultMessage),
                    FunctionalResultUnionCases.Failed => FunctionalResult<TFailure>.Failed(
                        serialized.Failure!.ToFailure()),
                    _ => throw new ArgumentOutOfRangeException($"Unexpected {nameof(FunctionalResultUnionCases)}: {serialized.FunctionalResult}")
                }),
            CommandResultUnionCases.Faulted => CommandResult<TFailure>.Faulted(commandId, serialized.ResultMessage, null),
            CommandResultUnionCases.Unhandled => CommandResult<TFailure>.Unhandled(commandId, serialized.ResultMessage),
            CommandResultUnionCases.Cancelled => CommandResult<TFailure>.Cancelled(commandId),
            _ => throw new ArgumentOutOfRangeException($"Unexpected {nameof(CommandResultUnionCases)}: {serialized.CommandResult}")
        });
    }

    protected override CommandProcessedPayload<TFailurePayload> MapToSerializablePayload(CommandProcessed<TFailure> payload)
    {
        var commandId = payload.CommandId.Id;
        return payload.CommandResult
            .Match(processed: p =>
                {
                    var (functionalResult, failure, message) =
                        p.FunctionalResult.Match(
                        ok => (functionalResult: FunctionalResultUnionCases.Ok, failure: default(TFailurePayload), message: (string?)ok.ResultMessage),
                        failed => (FunctionalResultUnionCases.Failed, TFailurePayload.FromFailure(failed.Failure), null)
                    );
                    
                    return new CommandProcessedPayload<TFailurePayload>(commandId, CommandResultUnionCases.Processed, functionalResult, failure, message);
                },
                faulted: f => new(commandId, CommandResultUnionCases.Faulted, null, null, f.Message),
                unhandled: u => new(commandId, CommandResultUnionCases.Unhandled, null, null, u.Message),
                cancelled: c => new(commandId, CommandResultUnionCases.Cancelled, null, null, null)
            );
    } }