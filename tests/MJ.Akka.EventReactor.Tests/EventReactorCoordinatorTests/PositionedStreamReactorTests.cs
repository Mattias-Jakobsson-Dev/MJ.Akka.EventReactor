using System.Collections.Concurrent;
using System.Collections.Immutable;
using Akka;
using Akka.Actor;
using Akka.Persistence;
using Akka.Streams.Dsl;
using MJ.Akka.EventReactor.PositionStreamSource;
using MJ.Akka.EventReactor.Setup;
using MJ.Akka.EventReactor.Tests.TestData;
using Xunit;

namespace MJ.Akka.EventReactor.Tests.EventReactorCoordinatorTests;

public class PositionedStreamReactorTests(NormalTestKitActorSystem systemHandler) 
    : EventReactorCoordinatorTestsBase(systemHandler), IClassFixture<NormalTestKitActorSystem>
{
    protected override ITestReactor CreateReactor(
        IImmutableList<(Events.IEvent, IImmutableDictionary<string, object?>)> events,
        ActorSystem actorSystem,
        string? name = null)
    {
        return new PositionedStreamReactor(events, actorSystem, name);
    }
    
    private class PositionedStreamReactor(
        IImmutableList<(Events.IEvent, IImmutableDictionary<string, object?>)> events,
        ActorSystem actorSystem, 
        string? name = null) 
        : ITestReactor
    {
        private readonly ConcurrentDictionary<string, int> _handledEvents = [];
        
        public string Name => !string.IsNullOrEmpty(name) ? name : GetType().Name;
        
        public ISetupEventReactor Configure(ISetupEventReactor config)
        {
            return TestReactor.ConfigureHandlers(config, _handledEvents);
        }

        public Task<IEventReactorEventSource> GetSource()
        {
            return Task.FromResult<IEventReactorEventSource>(new PositionedStreamEventReactorEventSource(
                new PositionStreamStarter(events),
                actorSystem,
                this));
        }
        
        public async Task<IImmutableList<string>> GetDeadLetters()
        {
            return (await StoredEventsLoader.GetEventsFrom(actorSystem, $"event-reactor-dead-letters-{Name}"))
                .OfType<DeadLetterHandler.Events.DeadLetterAdded>()
                .Select(x => x.Event)
                .OfType<Events.IEvent>()
                .Select(x => x.EventId)
                .ToImmutableList();
        }

        public IImmutableDictionary<string, int> GetHandledEvents()
        {
            return _handledEvents.ToImmutableDictionary();
        }
        
        private class PositionStreamStarter(
            IImmutableList<(Events.IEvent evnt, IImmutableDictionary<string, object?> metadata)> events) : IStartPositionStream
        {
            public Source<EventWithPosition, NotUsed> StartFrom(long? position)
            {
                return Source
                    .From(events.Select((evnt, index) => new EventWithPosition(evnt.evnt, evnt.metadata, index + 1)))
                    .Where(x => position == null || x.Position > position);
            }

            public Task<long?> GetInitialPosition()
            {
                return Task.FromResult<long?>(null);
            }
        }
    }
    
    private class StoredEventsLoader : ReceivePersistentActor
    {
        private static class Queries
        {
            public record GetEvents;
        }

        private static class Responses
        {
            public record GetEventsResponse(IImmutableList<object> Events);
        }
        
        private readonly List<object> _events = [];
        
        public override string PersistenceId { get; }

        public StoredEventsLoader(string id)
        {
            PersistenceId = id;
            
            Recover<object>(evnt => _events.Add(evnt));
            
            Command<Queries.GetEvents>(_ =>
            {
                Sender.Tell(new Responses.GetEventsResponse(_events.ToImmutableList()));
            });
        }

        public static async Task<IImmutableList<object>> GetEventsFrom(ActorSystem actorSystem, string id)
        {
            var actor = actorSystem.ActorOf(Props.Create(() => new StoredEventsLoader(id)));
            
            var response = await actor.Ask<Responses.GetEventsResponse>(new Queries.GetEvents());
            
            actorSystem.Stop(actor);
            
            return response.Events;
        }
    }
}