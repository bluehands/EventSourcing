using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using EventSourcing.Funicular.Commands.SerializablePayloads;

namespace EventSourcing.Funicular.Commands.Templates.SerializablePayloads;

public record FailurePayload(FailurePayload.UnionCases UnionCase, string? Message, IReadOnlyCollection<FailurePayload>? ChildFailures = null)
    : IFailurePayload<Templates.FailureTypeName, FailurePayload>
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

    public Templates.FailureTypeName ToFailure() => ToFailure(this);

    public static FailurePayload FromFailure(Templates.FailureTypeName failure) =>
        failure.Match(
            cancelled: _ => new(UnionCase: UnionCases.Cancelled, Message: failure.Message),
            conflict: _ => new(UnionCase: UnionCases.Conflict, Message: failure.Message),
            forbidden: _ => new(UnionCase: UnionCases.Forbidden, Message: failure.Message),
            @internal: _ => new(UnionCase: UnionCases.Internal, Message: failure.Message),
            invalidInput: _ => new(UnionCase: UnionCases.InvalidInput, Message: failure.Message),
            notFound: _ => new(UnionCase: UnionCases.NotFound, Message: failure.Message),
            multiple: m => new FailurePayload(UnionCase: UnionCases.Multiple, Message: null, m.Failures.Select(FromFailure).ToImmutableArray())
        );

    public static Templates.FailureTypeName ToFailure(FailurePayload payload) =>
        payload.UnionCase switch
        {
            UnionCases.Cancelled => Templates.FailureTypeName.Cancelled(payload.Message!),
            UnionCases.Conflict => Templates.FailureTypeName.Conflict(payload.Message!),
            UnionCases.Forbidden => Templates.FailureTypeName.Forbidden(payload.Message!),
            UnionCases.Internal => Templates.FailureTypeName.Internal(payload.Message!),
            UnionCases.InvalidInput => Templates.FailureTypeName.InvalidInput(payload.Message!),
            UnionCases.NotFound => Templates.FailureTypeName.NotFound(payload.Message!),
            UnionCases.Multiple => Templates.FailureTypeName.Multiple(payload.ChildFailures!.Select(ToFailure)
                .ToImmutableArray()),
            _ => throw new ArgumentOutOfRangeException(nameof(payload), payload.UnionCase, ""),
        };
}