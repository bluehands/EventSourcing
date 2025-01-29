namespace EventSourcing.Funicular.Commands.SerializablePayloads;

public record ResultPayload<TFailurePayload>(
    ResultUnionCases UnionCase,
    TFailurePayload? Failure)
    where TFailurePayload : class;

public enum ResultUnionCases
{
    Ok,
    Error
}