using Akka;
using Akka.Persistence.EventStore.Streams;
using Akka.Streams.Dsl;
using EventStore.Client;

namespace DC.Akka.EventReactor.EventStore;

public abstract class EventStorePersistentSubscriptionEventReactor(
    EventStorePersistentSubscriptionsClient client,
    string streamName,
    string groupName,
    int maxBufferSize = 500,
    bool keepReconnecting = false,
    int serializationParallelism = 10) : IEventReactor
{
    public abstract string Name { get; }

    public abstract void Configure(Func<ISetupEventReactor, ISetupEventReactor> config);

    public Source<IMessageWithAck, NotUsed> StartSource()
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
                    DeSerializedEvent = await DeSerialize(evnt.Event),
                    SourceEvent = evnt
                })
            .Where(x => x.DeSerializedEvent != null)
            .Select(IMessageWithAck (x) => new EventStoreMessage(
                x.DeSerializedEvent!,
                x.SourceEvent.Ack,
                e => x.SourceEvent.Nack(e.Message)));
    }

    protected abstract Task<object?> DeSerialize(ResolvedEvent evnt);

    private class EventStoreMessage(object message, Func<Task> ack, Func<Exception, Task> nack) : IMessageWithAck
    {
        public object Message { get; } = message;

        public Task Ack()
        {
            return ack();
        }

        public Task Nack(Exception error)
        {
            return nack(error);
        }
    }
}