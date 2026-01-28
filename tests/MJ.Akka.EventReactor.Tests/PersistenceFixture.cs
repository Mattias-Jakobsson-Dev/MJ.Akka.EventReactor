using System.Collections.Immutable;
using Akka.Actor;
using Akka.Persistence;
using Akka.Persistence.TestKit;
using Xunit;

namespace MJ.Akka.EventReactor.Tests;

public abstract class PersistenceFixture : PersistenceTestKit, IAsyncLifetime
{
    public IImmutableList<StoredEventsInterceptor.StoredEvent> StoredEvents { get; private set; } =
        ImmutableList<StoredEventsInterceptor.StoredEvent>.Empty;
    
    public async Task InitializeAsync()
    {
        await Setup();

        var interceptor = new StoredEventsInterceptor();

        await WithJournalWrite(
            x => x.SetInterceptorAsync(interceptor),
            async () =>
            {
                await Run();
            });

        StoredEvents = interceptor.StoredEvents;
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    protected abstract Task Setup();

    protected abstract Task Run();
    
    protected async Task WithEvents(string persistenceId, IImmutableList<object> events, int startSequenceNumber = 1)
    {
        var probe = CreateTestProbe();
        
        var messages = events
            .Select((evnt, index) => new AtomicWrite(new Persistent(
                evnt,
                startSequenceNumber + index,
                persistenceId)))
            .OfType<IPersistentEnvelope>()
            .ToImmutableList();
        
        JournalActorRef.Tell(new WriteMessages(messages, probe.Ref, 1));
        
        await probe.ExpectMsgAsync<WriteMessagesSuccessful>();

        foreach (var _ in events)
            await probe.ExpectMsgAsync<WriteMessageSuccess>();
    }

    protected async Task<ImmutableList<StoredEventsInterceptor.StoredEvent>> ReadEvents(string persistenceId)
    {
        var probe = CreateTestProbe();
        
        JournalActorRef.Tell(
            new ReplayMessages(
                0,
                long.MaxValue,
                long.MaxValue,
                persistenceId,
                probe.Ref));

        var result = new List<StoredEventsInterceptor.StoredEvent>();

        while (true)
        {
            var message = await probe.ExpectMsgAsync<object>();
            
            if (message is ReplayedMessage replayedMessage)
            {
                result.Add(new StoredEventsInterceptor.StoredEvent(
                    persistenceId,
                    replayedMessage.Persistent.Payload));
            }
            else if (message is RecoverySuccess)
            {
                break;
            }
            else
            {
                throw new Exception("Wrong message type");
            }
        }

        return result.ToImmutableList();
    }
}