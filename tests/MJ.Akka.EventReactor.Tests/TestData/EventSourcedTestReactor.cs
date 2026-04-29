using System.Collections.Concurrent;
using System.Collections.Immutable;
using Akka;
using Akka.Actor;
using Akka.Streams;
using Akka.Streams.Dsl;
using MJ.Akka.EventReactor.EventSourced;

namespace MJ.Akka.EventReactor.Tests.TestData;

public class EventSourcedTestReactor(
    ActorSystem actorSystem,
    IImmutableList<(Events.IEvent evnt, IImmutableDictionary<string, object?> metadata)> events,
    string? name = null) : EventSourcedEventReactor<TestState>(actorSystem), ITestReactor
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

    protected override ISetupEventSourcedEventReactor<TestState> Configure(
        ISetupEventSourcedEventReactor<TestState> config)
    {
        return ConfigureHandlers(config, _handledEvents);
    }

    protected override TestState GetDefaultState(string id)
    {
        return new TestState(string.Empty);
    }

    protected override void SetupEventAppliers(ISetupEventApplier<TestState> setup)
    {
        setup
            .On<Events.HandledEvent>((state, _) => state)
            .On<EventSourcedEvents.EventThatChangesState>((state, evnt) => 
                new TestState(evnt.NewName));
    }

    public static ISetupEventSourcedEventReactor<TestState> ConfigureHandlers(
        ISetupEventSourcedEventReactor<TestState> config,
        ConcurrentDictionary<string, int> handledEvents)
    {
        return config
            .On<Events.HandledEvent>(x => x.EntityId)
            .ReactWith(evnt => handledEvents
                .AddOrUpdate(evnt.EventId, _ => 1, (_, current) => current + 1))
            .TransformWith(evnt => ImmutableList.Create<object>(evnt))
            .On<EventSourcedEvents.EventThatChangesState>(x => x.EntityId)
            .ReactWith(evnt => handledEvents
                .AddOrUpdate(evnt.EventId, _ => 1, (_, current) => current + 1))
            .TransformWith(evnt => ImmutableList.Create<object>(evnt));
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



