using System.Reactive;

namespace EventSourcing.Funicular.Commands.JsonPayload;

public class CommandProcessedMapper<TFailure, TFailurePayload, TResult>
    : EventPayloadMapper<CommandProcessed<TFailure, TResult>, CommandProcessedPayload<TFailurePayload>>
    where TFailure : IFailure<TFailure>
	where TFailurePayload : class, IFailurePayload<TFailure, TFailurePayload>
	where TResult : IResult<Unit, TFailure, TResult>
{
    protected override CommandProcessed<TFailure, TResult> MapFromSerializablePayload(CommandProcessedPayload<TFailurePayload> serialized, StreamId streamId) =>
		new(CommandId: new(Id: serialized.CommandId),
			OperationResult: serialized.OperationResult.UnionCase == OperationResultUnionCases.Ok
				? TResult.Ok(value: Unit.Default)
				: TResult.Error(serialized.OperationResult.Failure!.ToFailure()),
			ResultMessage: serialized.ResultMessage);

	protected override CommandProcessedPayload<TFailurePayload> MapToSerializablePayload(CommandProcessed<TFailure, TResult> payload) => new(
		CommandId: payload.CommandId.Id,
		OperationResult: Map(source: payload.OperationResult),
		ResultMessage: payload.ResultMessage
	);

	private static OperationResultPayload<TFailurePayload> Map(IResult<Unit, TFailure> source)
	{
		var (unionCase, failure) = source.Match(
			ok: _ => (OperationResultUnionCases.Ok, (TFailurePayload?)null),
			error: error => (OperationResultUnionCases.Error, TFailurePayload.FromFailure(error))
		);
		return new(UnionCase: unionCase, Failure: failure);
	}
}