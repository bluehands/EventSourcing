namespace EventSourcing.Benchmarks;

public sealed record BenchmarkPayload(string Id, int Sequence, string Data)
    : EventPayload(new StreamId("Benchmark", Id), "Benchmark.Event.v1");

[SerializableEventPayload("Benchmark.Event.v1")]
public sealed record StoredBenchmarkPayload(int Sequence, string Data);

public sealed class BenchmarkPayloadMapper : EventPayloadMapper<BenchmarkPayload, StoredBenchmarkPayload>
{
    protected override BenchmarkPayload MapFromSerializablePayload(StoredBenchmarkPayload serialized, StreamId streamId) =>
        new(streamId.Id, serialized.Sequence, serialized.Data);

    protected override StoredBenchmarkPayload MapToSerializablePayload(BenchmarkPayload payload) =>
        new(payload.Sequence, payload.Data);
}
