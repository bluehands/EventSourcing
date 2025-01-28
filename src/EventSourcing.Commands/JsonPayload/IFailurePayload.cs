namespace EventSourcing.Funicular.Commands.JsonPayload;

public interface IFailurePayload<out TFailure>
{
    TFailure ToFailure();
}

public interface IFailurePayload<TFailure, out TFailurePayload> : IFailurePayload<TFailure>
{
    static abstract TFailurePayload FromFailure(TFailure failure);
}