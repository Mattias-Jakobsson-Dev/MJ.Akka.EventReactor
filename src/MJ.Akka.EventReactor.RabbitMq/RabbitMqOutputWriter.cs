using Akka;
using Akka.Streams.Amqp.RabbitMq;
using Akka.Streams.Dsl;
using JetBrains.Annotations;
using MJ.Akka.EventReactor.Configuration;

namespace MJ.Akka.EventReactor.RabbitMq;

[PublicAPI]
public class RabbitMqOutputWriter(
    AmqpSinkSettings settings,
    IRabbitMqMessageSerializer serializer,
    int serializationParallelism = 1) : IOutputWriter
{
    public Sink<object, NotUsed> CreateSink()
    {
        return Flow.Create<object>()
            .SelectAsync(
                serializationParallelism,
                serializer.Serialize)
            .ToMaterialized(new AmqpSinkStage(settings), Keep.Left);
    }
}