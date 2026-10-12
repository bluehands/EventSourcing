using EventSourcing.Commands.SerializablePayloads;

namespace EventSourcing.Benchmarks;

public record BenchmarkErrorPayload(string Message) : IErrorPayload<string, BenchmarkErrorPayload>
{
    public string ToError() => Message;
    public static BenchmarkErrorPayload FromError(string error) => new(error);
}
