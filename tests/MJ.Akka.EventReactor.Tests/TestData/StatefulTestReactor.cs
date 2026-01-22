using System.Collections.Concurrent;
using System.Collections.Immutable;
using Akka;
using Akka.Actor;
using Akka.Streams.Dsl;
using MJ.Akka.EventReactor.Stateful;

namespace MJ.Akka.EventReactor.Tests.TestData;

public class StatefulTestReactor(
    ActorSystem actorSystem,
    IStatefulReactorStorage storage,
    IImmutableList<(Events.IEvent evnt, IImmutableDictionary<string, object?> metadata)> events,
    string? name = null) : StatefulEventReactor<TestState>(actorSystem), IStatefulTestReactor
{
    private readonly ConcurrentDictionary<string, int> _handledEvents = [];
    private readonly EventSource _eventSource = new(events);
    
    public override string Name => !string.IsNullOrEmpty(name) ? name : GetType().Name;
    
    public override Task<IEventReactorEventSource> GetSource()
    {
        return Task.FromResult<IEventReactorEventSource>(_eventSource);
    }

    public IImmutableDictionary<string, int> GetHandledEvents()
    {
        return _handledEvents.ToImmutableDictionary();
    }

    public IStatefulReactorStorage GetStorage()
    {
        return storage;
    }

    protected override ISetupStatefulEventReactor<TestState> Configure(ISetupStatefulEventReactor<TestState> config)
    {
        return ConfigureHandlers(config, _handledEvents);
    }

    protected override IStatefulReactorStorage CreateStorage()
    {
        return storage;
    }

    protected override TestState? GetDefaultState(string? id)
    {
        return null;
    }
    
    public static ISetupStatefulEventReactor<TestState> ConfigureHandlers(
        ISetupStatefulEventReactor<TestState> config,
        ConcurrentDictionary<string, int> handledEvents)
    {
        var onceFailedEvents = new ConcurrentBag<string>();
        
        return config
            .On<Events.HandledEvent>(x => x.EntityId)
            .ReactWith(evnt => handledEvents
                .AddOrUpdate(evnt.EventId, _ => 1, (_, current) => current + 1))
            .On<Events.EventThatFails>(x => x.EntityId)
            .ReactWith(evnt => throw new Exception("Failed"))
            .On<Events.EventThatFailsOnce>(x => x.EntityId)
            .ReactWith(evnt =>
            {
                if (!onceFailedEvents.Contains(evnt.EventId))
                {
                    onceFailedEvents.Add(evnt.EventId);

                    throw new Exception("Failed");
                }

                handledEvents
                    .AddOrUpdate(evnt.EventId, _ => 1, (_, current) => current + 1);
            })
            .On<Events.TransformInto>(x => x.EntityId)
            .ReactWith(evnt => handledEvents
                .AddOrUpdate(evnt.EventId, _ => 1, (_, current) => current + 1))
            .TransformWith(evnt => evnt.Results)
            .On<StatefulEvents.EventThatModifiesState>(x => x.EntityId)
            .ReactWith(evnt => handledEvents
                .AddOrUpdate(evnt.EventId, _ => 1, (_, current) => current + 1))
            .ModifyState((state, evnt) => evnt.Modify(state));
    }
    
    private class EventSource(
        IImmutableList<(Events.IEvent evnt, IImmutableDictionary<string, object?> metadata)> events) : IEventReactorEventSource
    {
        private readonly ConcurrentBag<string> _eventsToSkip = [];
        
        public Source<IMessageWithAck, NotUsed> Start()
        {
            return Source.From(events.Where(x => !_eventsToSkip.Contains(x.evnt.EventId)))
                .Select(IMessageWithAck (x) => 
                    new EventWithAck(x.evnt, x.metadata, _eventsToSkip));
        }
    }
    
    private class EventWithAck(
        Events.IEvent evnt,
        IImmutableDictionary<string, object?> metadata,
        ConcurrentBag<string> eventsToSkip) : IMessageWithAck
    {
        public object Message { get; } = evnt;
        public IImmutableDictionary<string, object?> Metadata { get; } = metadata;

        public Task Ack(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            eventsToSkip.Add(evnt.EventId);
            
            return Task.CompletedTask;
        }

        public Task Nack(Exception error, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            eventsToSkip.Add(evnt.EventId);
            
            return Task.CompletedTask;
        }
    }
}