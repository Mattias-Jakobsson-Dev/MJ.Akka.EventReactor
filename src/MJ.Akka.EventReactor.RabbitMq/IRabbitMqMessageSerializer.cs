using Akka.Streams.Amqp.RabbitMq;

namespace MJ.Akka.EventReactor.RabbitMq;

public interface IRabbitMqMessageSerializer
{
    Task<OutgoingMessage> Serialize(object message);
    
    Task<object> DeSerialize(IncomingMessage message);
}