using System.Reactive;

namespace EventSourcing.Funicular.Commands.SerializablePayloads;

public class CommandProcessedMapper<TFailure, TFailurePayload, TResult>
    : EventPayloadMapper<CommandProcessed<TFailure, TResult>, CommandProcessedPayload<TFailurePayload>>
    where TFailure : IFailure<TFailure>
	where TFailurePayload : class, IFailurePayload<TFailure, TFailurePayload>
	where TResult : IResult<Unit, TFailure, TResult>
{
    protected override CommandProcessed<TFailure, TResult> MapFromSerializablePayload(CommandProcessedPayload<TFailurePayload> serialized, StreamId streamId) =>
		new(CommandId: new(Id: serialized.CommandId),
			Result: serialized.Result.UnionCase == ResultUnionCases.Ok
				? TResult.Ok(value: Unit.Default)
				: TResult.Error(serialized.Result.Failure!.ToFailure()),
			ResultMessage: serialized.ResultMessage);

	protected override CommandProcessedPayload<TFailurePayload> MapToSerializablePayload(CommandProcessed<TFailure, TResult> payload) => new(
		CommandId: payload.CommandId.Id,
		Result: Map(source: payload.Result),
		ResultMessage: payload.ResultMessage
	);

	private static ResultPayload<TFailurePayload> Map(IResult<Unit, TFailure> source)
	{
		var (unionCase, failure) = source.Match(
			ok: _ => (ResultUnionCases.Ok, (TFailurePayload?)null),
			error: error => (ResultUnionCases.Error, TFailurePayload.FromFailure(error))
		);
		return new(UnionCase: unionCase, Failure: failure);
	}
}