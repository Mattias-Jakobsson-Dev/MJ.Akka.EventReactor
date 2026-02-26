using System.Collections.Immutable;
using System.Text.Json;
using Akka.IO;
using Akka.Streams.Amqp.RabbitMq;
using JetBrains.Annotations;
using RabbitMQ.Client;

namespace MJ.Akka.EventReactor.RabbitMq;

[PublicAPI]
public class SerializeRabbitMqMessagesAsJson(Func<object, string?>? findRoutingKey = null) : IRabbitMqMessageSerializer
{
    public const string TypeMetadataKey = "message-type";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public Task<OutgoingMessage> Serialize(object message)
    {
        var json = JsonSerializer.Serialize(message, message.GetType(), Options);

        return Task.FromResult(new OutgoingMessage(
            ByteString.FromString(json),
            true,
            true,
            new BasicProperties
            {
                Headers = new Dictionary<string, object?>
                {
                    { TypeMetadataKey, message.GetType().AssemblyQualifiedName }
                }
            },
            routingKey: findRoutingKey?.Invoke(message)));
    }

    public Task<(object data, IImmutableDictionary<string, object?> metadata)> DeSerialize(IncomingMessage message)
    {
        var json = message.Bytes.ToString();

        var typeName = message.Properties.Headers?.TryGetValue(TypeMetadataKey, out var raw) == true
            ? raw?.ToString()
            : null;

        var type = typeName != null
            ? Type.GetType(typeName) ?? throw new SerializationException($"Could not resolve type '{typeName}'")
            : typeof(JsonElement);

        var result = JsonSerializer.Deserialize(json, type, Options)
                     ?? throw new SerializationException("Failed to deserialize RabbitMQ message");

        var metadata = ImmutableDictionary<string, object?>.Empty
            .Add(TypeMetadataKey, message.GetType().AssemblyQualifiedName);

        return Task.FromResult<(object data, IImmutableDictionary<string, object?> metadata)>(
            (result, metadata));
    }
}