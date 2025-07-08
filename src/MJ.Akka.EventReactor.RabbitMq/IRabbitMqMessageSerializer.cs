using System.Collections.Immutable;
using Akka.Streams.Amqp.RabbitMq;

namespace MJ.Akka.EventReactor.RabbitMq;

public interface IRabbitMqMessageSerializer
{
    Task<OutgoingMessage> Serialize(object message);
    
    Task<(object data, IImmutableDictionary<string, object?> metadata)> DeSerialize(IncomingMessage message);
}