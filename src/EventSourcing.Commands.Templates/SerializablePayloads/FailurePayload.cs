﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using EventSourcing.Commands.SerializablePayloads;

namespace EventSourcing.Commands.Templates.SerializablePayloads;

public record FailurePayload(FailurePayload.UnionCases UnionCase, string? Message, IReadOnlyCollection<FailurePayload>? ChildFailures = null)
    : IFailurePayload<FailureTypeName, FailurePayload>
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

    public FailureTypeName ToFailure() => ToFailure(this);

    public static FailurePayload FromFailure(FailureTypeName failure) =>
        failure.Match(
            cancelled: _ => new(UnionCase: UnionCases.Cancelled, Message: failure.Message),
            conflict: _ => new(UnionCase: UnionCases.Conflict, Message: failure.Message),
            forbidden: _ => new(UnionCase: UnionCases.Forbidden, Message: failure.Message),
            @internal: _ => new(UnionCase: UnionCases.Internal, Message: failure.Message),
            invalidInput: _ => new(UnionCase: UnionCases.InvalidInput, Message: failure.Message),
            notFound: _ => new(UnionCase: UnionCases.NotFound, Message: failure.Message),
            multiple: m => new FailurePayload(UnionCase: UnionCases.Multiple, Message: null, m.Failures.Select(FromFailure).ToImmutableArray())
        );

    public static FailureTypeName ToFailure(FailurePayload payload) =>
        payload.UnionCase switch
        {
            UnionCases.Cancelled => FailureTypeName.Cancelled(payload.Message!),
            UnionCases.Conflict => FailureTypeName.Conflict(payload.Message!),
            UnionCases.Forbidden => FailureTypeName.Forbidden(payload.Message!),
            UnionCases.Internal => FailureTypeName.Internal(payload.Message!),
            UnionCases.InvalidInput => FailureTypeName.InvalidInput(payload.Message!),
            UnionCases.NotFound => FailureTypeName.NotFound(payload.Message!),
            UnionCases.Multiple => FailureTypeName.Multiple(payload.ChildFailures!.Select(ToFailure)
                .ToImmutableArray()),
            _ => throw new ArgumentOutOfRangeException(nameof(payload), payload.UnionCase, ""),
        };
}