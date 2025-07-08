using System.Collections.Immutable;
using Akka;
using Akka.Streams.Amqp.RabbitMq;
using Akka.Streams.Amqp.RabbitMq.Dsl;
using Akka.Streams.Dsl;
using JetBrains.Annotations;

namespace MJ.Akka.EventReactor.RabbitMq;

[PublicAPI]
public class RabbitMqEventReactorSource(
    IAmqpSourceSettings settings,
    int bufferSize,
    IRabbitMqMessageSerializer serializer,
    int deSerializationParallelism = 1) : IEventReactorEventSource
{
    public Source<IMessageWithAck, NotUsed> Start()
    {
        return AmqpSource.CommittableSource(settings, bufferSize)
            .SelectAsync(
                deSerializationParallelism,
                async x =>
                {
                    var (message, metadata) = await serializer.DeSerialize(x.Message);

                    return (IMessageWithAck)new MessageWithAck(message, metadata, () => x.Ack(), () => x.Nack());
                });
    }

    private class MessageWithAck(
        object message,
        IImmutableDictionary<string, object?> metadata,
        Func<Task> ack, 
        Func<Task> nack) : IMessageWithAck
    {
        public object Message { get; } = message;
        public IImmutableDictionary<string, object?> Metadata { get; } = metadata;

        public Task Ack(CancellationToken cancellationToken)
        {
            return ack();
        }

        public Task Nack(Exception error, CancellationToken cancellationToken)
        {
            return nack();
        }
    }
}