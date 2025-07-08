using System.Collections.Immutable;
using Akka.IO;
using Akka.Streams.Amqp.RabbitMq;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace MJ.Akka.EventReactor.RabbitMq;

[PublicAPI]
public class SerializeRabbitMqMessagesAsJson(Func<object, string?>? findRoutingKey = null) : IRabbitMqMessageSerializer
{
    private readonly JsonSerializerSettings _settings = new()
    {
        TypeNameHandling = TypeNameHandling.All
    };
    
    public Task<OutgoingMessage> Serialize(object message)
    {
        var json = JsonConvert.SerializeObject(message, _settings);
        
        return Task.FromResult(new OutgoingMessage(
            ByteString.FromString(json),
            true, 
            true,
            routingKey: findRoutingKey?.Invoke(message)));
    }

    public Task<(object data, IImmutableDictionary<string, object?> metadata)> DeSerialize(IncomingMessage message)
    {
        var result = JsonConvert.DeserializeObject(message.Bytes.ToString(), _settings);
        
        if (result == null)
            throw new SerializationException("Failed to deserialize RabbitMQ message");

        return Task.FromResult<(object data, IImmutableDictionary<string, object?> metadata)>(
            (result, ImmutableDictionary<string, object?>.Empty));
    }
}