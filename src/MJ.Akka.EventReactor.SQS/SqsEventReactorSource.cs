using System.Collections.Immutable;
using Akka;
using Akka.Streams.Dsl;
using Akka.Streams.SQS;
using Amazon.SQS;
using JetBrains.Annotations;

namespace MJ.Akka.EventReactor.SQS;

[PublicAPI]
public class SqsEventReactorSource(
    IAmazonSQS client,
    string queueUrl,
    ISqsMessageSerializer? serializer = null,
    int deSerializationParallelism = 1,
    SqsSourceSettings? settings = null) : IEventReactorEventSource
{
    public Source<IMessageWithAck, NotUsed> Start()
    {
        var serializerToUse = serializer ?? new SerializeSqsMessagesAsJson();
        
        return SqsSource
            .Create(client, queueUrl, settings)
            .SelectAsync(
                deSerializationParallelism,
                async x =>
                {
                    var (message, metadata) = await serializerToUse.DeSerialize(x);

                    return (IMessageWithAck)new MessageWithAck(
                        message,
                        metadata, 
                        () => client.DeleteMessageAsync(queueUrl, x.ReceiptHandle),
                        () => Task.CompletedTask);
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