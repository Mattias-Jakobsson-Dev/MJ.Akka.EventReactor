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
    Func<ResolvedEvent, Task<object?>> deSerialize,
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
            return result?.Payload;
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
            .Where(x => x.DeSerializedEvent != null)
            .Select(IMessageWithAck (x) => new EventStoreMessage(
                x.DeSerializedEvent!,
                x.SourceEvent.Ack,
                e => x.SourceEvent.Nack(e.Message)));
    }
    
    private class EventStoreMessage(object message, Func<Task> ack, Func<Exception, Task> nack) : IMessageWithAck
    {
        public object Message { get; } = message;

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
