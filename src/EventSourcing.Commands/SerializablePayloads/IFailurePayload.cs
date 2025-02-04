namespace EventSourcing.Commands.SerializablePayloads;

public interface IFailurePayload<out TFailure>
{
    TFailure ToFailure();
}

public interface IFailurePayload<TFailure, out TFailurePayload> : IFailurePayload<TFailure>
{
    static abstract TFailurePayload FromFailure(TFailure failure);
}