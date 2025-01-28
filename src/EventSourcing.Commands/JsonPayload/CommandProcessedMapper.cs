using System.Reactive;

namespace EventSourcing.Funicular.Commands.JsonPayload;

public class CommandProcessedMapper<TFailure, TFailurePayload, TOperationResult>
    : EventPayloadMapper<CommandProcessed<TFailure, TOperationResult>, CommandProcessedPayload<TFailurePayload>>
    where TFailure : IFailure<TFailure>
	where TFailurePayload : class, IFailurePayload<TFailure, TFailurePayload>
	where TOperationResult : IResult<Unit, TFailure, TOperationResult>
{
    protected override Commands.CommandProcessed<TFailure, TOperationResult> MapFromSerializablePayload(CommandProcessedPayload<TFailurePayload> serialized, StreamId streamId) =>
		new(CommandId: new(Id: serialized.CommandId),
			OperationResult: serialized.OperationResult.UnionCase == OperationResultUnionCases.Ok
				? TOperationResult.Ok(value: Unit.Default)
				: TOperationResult.Error(serialized.OperationResult.Failure!.ToFailure()),
			ResultMessage: serialized.ResultMessage);

	protected override CommandProcessedPayload<TFailurePayload> MapToSerializablePayload(Commands.CommandProcessed<TFailure, TOperationResult> payload) => new(
		CommandId: payload.CommandId.Id,
		OperationResult: Map(source: payload.OperationResult),
		ResultMessage: payload.ResultMessage
	);

	private OperationResultPayload<TFailurePayload> Map(IResult<Unit, TFailure> source)
	{
		var (unionCase, failure) = source.Match(
			ok: _ => (OperationResultUnionCases.Ok, (TFailurePayload?)null),
			error: error => (OperationResultUnionCases.Error, TFailurePayload.FromFailure(error))
		);
		return new(UnionCase: unionCase, Failure: failure, Value: false);
	}
}