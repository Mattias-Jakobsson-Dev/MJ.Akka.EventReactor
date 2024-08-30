using Akka.Persistence.EventStore.Serialization;
using EventStore.Client;

namespace DC.Akka.EventReactor.EventStore;

public abstract class EventStorePersistentSubscriptionEventReactorWithAdapter(
    EventStorePersistentSubscriptionsClient client,
    string streamName,
    string groupName,
    IMessageAdapter adapter,
    int maxBufferSize = 500,
    bool keepReconnecting = false,
    int serializationParallelism = 10) : EventStorePersistentSubscriptionEventReactor(
    client,
    streamName,
    groupName,
    maxBufferSize,
    keepReconnecting,
    serializationParallelism)
{
    protected override async Task<object?> DeSerialize(ResolvedEvent evnt)
    {
        var result = await adapter.AdaptEvent(evnt);

        return result?.Payload;
    }
}