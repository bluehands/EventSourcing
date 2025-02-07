using System;

namespace EventSourcing.Commands.SerializablePayloads;

public class CommandProcessedMapper<TError, TErrorPayload>
    : EventPayloadMapper<CommandProcessed<TError>, CommandProcessedPayload<TErrorPayload>>
    where TErrorPayload : class, IErrorPayload<TError, TErrorPayload> where TError : notnull
{
    protected override CommandProcessed<TError> MapFromSerializablePayload(CommandProcessedPayload<TErrorPayload> serialized, StreamId streamId)
    {
        var commandId = new CommandId(serialized.CommandId);
        return new(serialized.CommandResult switch
        {
            CommandResultUnionCases.Processed => CommandResult<TError>.Processed(commandId,
                serialized.FunctionalResult switch
                {
                    FunctionalResultUnionCases.Ok => FunctionalResult<TError>.Ok(serialized.ResultMessage),
                    FunctionalResultUnionCases.Failed => FunctionalResult<TError>.Failed(
                        serialized.Error!.ToError()),
                    _ => throw new ArgumentOutOfRangeException($"Unexpected {nameof(FunctionalResultUnionCases)}: {serialized.FunctionalResult}")
                }),
            CommandResultUnionCases.Faulted => CommandResult<TError>.Faulted(commandId, serialized.ResultMessage, null),
            CommandResultUnionCases.Unhandled => CommandResult<TError>.Unhandled(commandId, serialized.ResultMessage),
            CommandResultUnionCases.Cancelled => CommandResult<TError>.Cancelled(commandId),
            _ => throw new ArgumentOutOfRangeException($"Unexpected {nameof(CommandResultUnionCases)}: {serialized.CommandResult}")
        });
    }

    protected override CommandProcessedPayload<TErrorPayload> MapToSerializablePayload(CommandProcessed<TError> payload)
    {
        var commandId = payload.CommandId.Id;
        return payload.CommandResult
            .Match(processed: p =>
                {
                    var (functionalResult, error, message) =
                        p.FunctionalResult.Match(
                        ok => (functionalResult: FunctionalResultUnionCases.Ok, error: default(TErrorPayload), message: (string?)ok.ResultMessage),
                        failed => (FunctionalResultUnionCases.Failed, TErrorPayload.FromError(failed.Error), null)
                    );
                    
                    return new CommandProcessedPayload<TErrorPayload>(commandId, CommandResultUnionCases.Processed, functionalResult, error, message);
                },
                faulted: f => new(commandId, CommandResultUnionCases.Faulted, null, null, f.Message),
                unhandled: u => new(commandId, CommandResultUnionCases.Unhandled, null, null, u.Message),
                cancelled: c => new(commandId, CommandResultUnionCases.Cancelled, null, null, null)
            );
    } }