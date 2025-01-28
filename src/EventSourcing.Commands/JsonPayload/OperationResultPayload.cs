namespace EventSourcing.Funicular.Commands.JsonPayload;

public record OperationResultPayload<TFailurePayload>(
    OperationResultUnionCases UnionCase,
    TFailurePayload? Failure,
    bool Value) // TODO: TResultPayload => need to have specific handling for this?
    where TFailurePayload : class;

public enum OperationResultUnionCases
{
    Ok,
    Error
}