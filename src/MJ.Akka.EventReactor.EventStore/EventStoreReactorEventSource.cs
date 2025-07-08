using System.Collections.Immutable;
using Akka;
using Akka.Persistence.EventStore.Serialization;
using Akka.Persistence.EventStore.Streams;
using Akka.Streams.Dsl;
using EventStore.Client;
using JetBrains.Annotations;

namespace MJ.Akka.EventReactor.EventStore;

[PublicAPI]
public class EventStoreReactorEventSource(
    EventStorePersistentSubscriptionsClient client,
    string streamName,
    string groupName,
    Func<ResolvedEvent, Task<(object data, IImmutableDictionary<string, object?> metadata)>> deSerialize,
    int maxBufferSize = 500,
    bool keepReconnecting = false,
    int serializationParallelism = 10) : IEventReactorEventSource
{
    public EventStoreReactorEventSource(
        EventStorePersistentSubscriptionsClient client,
        string streamName,
        string groupName,
        IMessageAdapter adapter,
        int maxBufferSize = 500,
        bool keepReconnecting = false,
        int serializationParallelism = 10) : this(
        client,
        streamName,
        groupName,
        async evnt =>
        {
            var result = await adapter.AdaptEvent(evnt);

            if (result == null)
                throw new SerializationException("Failed to deserialize event from EventStore");
            
            return (result.Payload, new Dictionary<string, object?>
            {
                [EventStoreMetadataKeys.PersistenceId] = result.PersistenceId,
                [EventStoreMetadataKeys.SequenceNr] = result.SequenceNr,
                [EventStoreMetadataKeys.Manifest] = result.Manifest,
                [EventStoreMetadataKeys.Timestamp] = result.Timestamp,
                [EventStoreMetadataKeys.WriterGuid] = result.WriterGuid,
                [EventStoreMetadataKeys.Sender] = result.Sender?.Path.ToString()
            }.ToImmutableDictionary());
        },
        maxBufferSize,
        keepReconnecting,
        serializationParallelism)
    {
        
    }
    
    public Source<IMessageWithAck, NotUsed> Start()
    {
        return EventStoreSource
            .ForPersistentSubscription(
                client,
                streamName,
                groupName,
                maxBufferSize,
                keepReconnecting)
            .SelectAsyncUnordered(
                serializationParallelism,
                async evnt => new
                {
                    DeSerializedEvent = await deSerialize(evnt.Event),
                    SourceEvent = evnt
                })
            .Select(IMessageWithAck (x) => new EventStoreMessage(
                x.DeSerializedEvent.data,
                x.DeSerializedEvent.metadata,
                x.SourceEvent.Ack,
                e => x.SourceEvent.Nack(e.Message)));
    }
    
    private class EventStoreMessage(
        object message,
        IImmutableDictionary<string, object?> metadata,
        Func<Task> ack,
        Func<Exception, Task> nack) : IMessageWithAck
    {
        public object Message { get; } = message;
        public IImmutableDictionary<string, object?> Metadata { get; } = metadata;

        public Task Ack(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            return ack();
        }

        public Task Nack(Exception error, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            return nack(error);
        }
    }
}
