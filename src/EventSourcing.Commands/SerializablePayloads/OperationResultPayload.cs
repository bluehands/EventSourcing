namespace EventSourcing.Funicular.Commands.SerializablePayloads;

public record OperationResultPayload<TFailurePayload>(
    OperationResultUnionCases UnionCase,
    TFailurePayload? Failure)
    where TFailurePayload : class;

public enum OperationResultUnionCases
{
    Ok,
    Error
}