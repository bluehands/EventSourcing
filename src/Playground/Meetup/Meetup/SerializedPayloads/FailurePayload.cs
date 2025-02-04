using System.Collections.Immutable;
using EventSourcing.Commands.SerializablePayloads;

namespace Meetup.SerializedPayloads;

public record FailurePayload(FailurePayload.UnionCases UnionCase, string? Message, IReadOnlyCollection<FailurePayload>? ChildFailures = null)
    : IFailurePayload<Failure, FailurePayload>
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

    public Failure ToFailure() => ToFailure(this);

    public static FailurePayload FromFailure(Failure failure) =>
        failure.Match(
            cancelled: _ => new(UnionCase: UnionCases.Cancelled, Message: failure.Message),
            conflict: _ => new(UnionCase: UnionCases.Conflict, Message: failure.Message),
            forbidden: _ => new(UnionCase: UnionCases.Forbidden, Message: failure.Message),
            @internal: _ => new(UnionCase: UnionCases.Internal, Message: failure.Message),
            invalidInput: _ => new(UnionCase: UnionCases.InvalidInput, Message: failure.Message),
            notFound: _ => new(UnionCase: UnionCases.NotFound, Message: failure.Message),
            multiple: m => new FailurePayload(UnionCase: UnionCases.Multiple, Message: null, m.Failures.Select(FromFailure).ToImmutableArray())
        );

    public static Failure ToFailure(FailurePayload payload) =>
        payload.UnionCase switch
        {
            UnionCases.Cancelled => Failure.Cancelled(payload.Message!),
            UnionCases.Conflict => Failure.Conflict(payload.Message!),
            UnionCases.Forbidden => Failure.Forbidden(payload.Message!),
            UnionCases.Internal => Failure.Internal(payload.Message!),
            UnionCases.InvalidInput => Failure.InvalidInput(payload.Message!),
            UnionCases.NotFound => Failure.NotFound(payload.Message!),
            UnionCases.Multiple => Failure.Multiple(payload.ChildFailures!.Select(ToFailure)
                .ToImmutableArray()),
            _ => throw new ArgumentOutOfRangeException(nameof(payload), payload.UnionCase, ""),
        };
}