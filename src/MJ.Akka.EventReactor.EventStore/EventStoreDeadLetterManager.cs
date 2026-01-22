using System.Collections.Immutable;
using Akka.Actor;
using Akka.Persistence.EventStore.Streams;
using Akka.Streams;
using Akka.Streams.Dsl;
using EventStore.Client;
using MJ.Akka.EventReactor.DeadLetter;

namespace MJ.Akka.EventReactor.EventStore;

public class EventStoreDeadLetterManager(
    EventStoreClient client,
    EventStorePersistentSubscriptionsClient subscriptionClient,
    string streamName,
    string groupName,
    Func<ResolvedEvent, Task<(object data, IImmutableDictionary<string, object?> metadata)>> deSerialize,
    int serializationParallelism,
    ActorSystem actorSystem) : IDeadLetterManager
{
    public async Task<IImmutableList<DeadLetterData>> LoadDeadLetters(long from, int count)
    {
        return await EventStoreSource
            .FromStream(
                client,
                new ParkedEventsStreamOrigin(
                    streamName,
                    groupName,
                    StreamPosition.FromInt64(from)))
            .SelectAsync(
                serializationParallelism,
                async evnt => new
                {
                    DeSerializedEvent = await deSerialize(evnt),
                    SourceEvent = evnt
                })
            .Take(count)
            .RunAggregate(
                ImmutableList<DeadLetterData>.Empty,
                (list, data) => list.Add(new DeadLetterData(
                    data.SourceEvent.Event.EventNumber.ToInt64(),
                    data.DeSerializedEvent.data,
                    data.DeSerializedEvent.metadata,
                    "")),
                actorSystem.Materializer());
    }

    public Task Retry(int count)
    {
        return subscriptionClient
            .ReplayParkedMessagesToStreamAsync(
                streamName,
                groupName,
                count);
    }

    public async Task Clear(long to)
    {
        var parkedStreamName = ParkedEventsStreamOrigin.GetStreamName(streamName, groupName);
        
        var metadata = await client.GetStreamMetadataAsync(parkedStreamName);
        
        var truncatePosition = StreamPosition.FromInt64(to);
        
        if (metadata.Metadata.TruncateBefore != null && metadata.Metadata.TruncateBefore >= truncatePosition)
            return;

        await client
            .SetStreamMetadataAsync(
                parkedStreamName,
                StreamState.Any,
                new StreamMetadata(
                    metadata.Metadata.MaxCount,
                    metadata.Metadata.MaxAge,
                    truncatePosition,
                    metadata.Metadata.CacheControl,
                    metadata.Metadata.Acl));
    }
    
    private class ParkedEventsStreamOrigin(
        string streamName,
        string groupName,
        StreamPosition from,
        Direction direction = Direction.Forwards) : IEventStoreStreamOrigin
    {
        public string StreamName { get; } = GetStreamName(streamName, groupName);
        public StreamPosition From { get; } = from;
        public Direction Direction { get; } = direction;
        
        public static string GetStreamName(string streamName, string groupName)
        {
            return $"$persistentsubscription-{streamName}::{groupName}-parked";
        }
    }
}