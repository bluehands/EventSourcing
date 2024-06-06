using EventSourcing;

namespace TaskBoardDemo.SerializablePayloads;

[SerializableEventPayload(EventTypes.TaskAdded)]
public record TaskAdded(string Name, string Description, string? Author);

public class  TaskAddedMapper : EventPayloadMapper<TaskBoardDemo.TaskAdded, TaskAdded>
{
    protected override TaskBoardDemo.TaskAdded MapFromSerializablePayload(TaskAdded serialized, StreamId streamId) => new (streamId.Id, serialized.Name, new (serialized.Description, serialized.Author ?? "not specified"));

    protected override TaskAdded MapToSerializablePayload(TaskBoardDemo.TaskAdded payload) => new (payload.Name, payload.Description.Description, payload.Description.Author);
}
