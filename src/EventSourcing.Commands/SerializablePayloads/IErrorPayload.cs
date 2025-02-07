namespace EventSourcing.Commands.SerializablePayloads;

public interface IErrorPayload<out TError>
{
    TError ToError();
}

public interface IErrorPayload<TError, out TErrorPayload> : IErrorPayload<TError>
{
    static abstract TErrorPayload FromError(TError error);
}