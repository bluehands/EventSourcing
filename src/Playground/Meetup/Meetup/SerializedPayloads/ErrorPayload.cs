using System.Collections.Immutable;
using EventSourcing.Commands.SerializablePayloads;

namespace Meetup.SerializedPayloads;

public record ErrorPayload(ErrorPayload.UnionCases UnionCase, string? Message, IReadOnlyCollection<ErrorPayload>? ChildErrors = null)
    : IErrorPayload<Error, ErrorPayload>
{
    public enum UnionCases
    {
        Forbidden,
        NotFound,
        Conflict,
        Internal,
        InvalidInput,
        Cancelled,
        Multiple
    }

    public Error ToError() => ToError(this);

    public static ErrorPayload FromError(Error error) =>
        error.Match(
            cancelled: _ => new(UnionCase: UnionCases.Cancelled, Message: error.Message),
            conflict: _ => new(UnionCase: UnionCases.Conflict, Message: error.Message),
            forbidden: _ => new(UnionCase: UnionCases.Forbidden, Message: error.Message),
            @internal: _ => new(UnionCase: UnionCases.Internal, Message: error.Message),
            invalidInput: _ => new(UnionCase: UnionCases.InvalidInput, Message: error.Message),
            notFound: _ => new(UnionCase: UnionCases.NotFound, Message: error.Message),
            multiple: m => new ErrorPayload(UnionCase: UnionCases.Multiple, Message: null, m.Errors.Select(FromError).ToImmutableArray())
        );

    public static Error ToError(ErrorPayload payload) =>
        payload.UnionCase switch
        {
            UnionCases.Cancelled => Error.Cancelled(payload.Message!),
            UnionCases.Conflict => Error.Conflict(payload.Message!),
            UnionCases.Forbidden => Error.Forbidden(payload.Message!),
            UnionCases.Internal => Error.Internal(payload.Message!),
            UnionCases.InvalidInput => Error.InvalidInput(payload.Message!),
            UnionCases.NotFound => Error.NotFound(payload.Message!),
            UnionCases.Multiple => Error.Multiple(payload.ChildErrors!.Select(ToError)
                .ToImmutableArray()),
            _ => throw new ArgumentOutOfRangeException(nameof(payload), payload.UnionCase, ""),
        };
}