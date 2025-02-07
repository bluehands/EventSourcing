using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using EventSourcing.Commands.SerializablePayloads;

namespace EventSourcing.Commands.Templates.SerializablePayloads;

public record ErrorPayload(ErrorPayload.UnionCases UnionCase, string? Message, IReadOnlyCollection<ErrorPayload>? ChildErrors = null)
    : IErrorPayload<ErrorTypeName, ErrorPayload>
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

    public ErrorTypeName ToError() => ToError(this);

    public static ErrorPayload FromError(ErrorTypeName error) =>
        error.Match(
            cancelled: _ => new(UnionCase: UnionCases.Cancelled, Message: error.Message),
            conflict: _ => new(UnionCase: UnionCases.Conflict, Message: error.Message),
            forbidden: _ => new(UnionCase: UnionCases.Forbidden, Message: error.Message),
            @internal: _ => new(UnionCase: UnionCases.Internal, Message: error.Message),
            invalidInput: _ => new(UnionCase: UnionCases.InvalidInput, Message: error.Message),
            notFound: _ => new(UnionCase: UnionCases.NotFound, Message: error.Message),
            multiple: m => new ErrorPayload(UnionCase: UnionCases.Multiple, Message: null, m.Errors.Select(FromError).ToImmutableArray())
        );

    public static ErrorTypeName ToError(ErrorPayload payload) =>
        payload.UnionCase switch
        {
            UnionCases.Cancelled => ErrorTypeName.Cancelled(payload.Message!),
            UnionCases.Conflict => ErrorTypeName.Conflict(payload.Message!),
            UnionCases.Forbidden => ErrorTypeName.Forbidden(payload.Message!),
            UnionCases.Internal => ErrorTypeName.Internal(payload.Message!),
            UnionCases.InvalidInput => ErrorTypeName.InvalidInput(payload.Message!),
            UnionCases.NotFound => ErrorTypeName.NotFound(payload.Message!),
            UnionCases.Multiple => ErrorTypeName.Multiple(payload.ChildErrors!.Select(ToError)
                .ToImmutableArray()),
            _ => throw new ArgumentOutOfRangeException(nameof(payload), payload.UnionCase, ""),
        };
}