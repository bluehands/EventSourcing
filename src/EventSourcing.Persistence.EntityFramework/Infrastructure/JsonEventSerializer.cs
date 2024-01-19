using EventSourcing.Infrastructure;

namespace EventSourcing.Persistence.EntityFramework.Infrastructure;

class JsonEventSerializer : IEventSerializer<string>
{
    public string Serialize(object serializablePayload) => System.Text.Json.JsonSerializer.Serialize(serializablePayload);

    public object Deserialize(Type serializablePayloadType, string serializedPayload) => System.Text.Json.JsonSerializer.Deserialize(serializedPayload, serializablePayloadType)!;
}