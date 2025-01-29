namespace EventSourcing.Funicular.Commands.JsonPayload;

public record OperationResultPayload<TFailurePayload>(
    OperationResultUnionCases UnionCase,
    TFailurePayload? Failure,
    bool Value)
    where TFailurePayload : class;

public enum OperationResultUnionCases
{
    Ok,
    Error
}