using EventSourcing;

namespace TaskBoardDemo.SerializablePayloads;

[SerializableEventPayload(EventTypes.TaskAdded)]
public record TaskAdded(string Name, string? Title, string? Abstract);

public class TaskAddedPayloadMapper : EventPayloadMapper<TaskBoardDemo.TaskAdded, TaskAdded>
{
    protected override TaskBoardDemo.TaskAdded MapFromSerializablePayload(TaskAdded serialized, StreamId streamId) => new (serialized.Name, serialized.Title ?? serialized.Abstract!);

    protected override TaskAdded MapToSerializablePayload(TaskBoardDemo.TaskAdded payload) => new (payload.Name, payload.Title, null);
}