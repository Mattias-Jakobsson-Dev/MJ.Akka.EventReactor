using System.Collections.Concurrent;
using System.Collections.Immutable;
using Akka;
using Akka.Streams.Dsl;
using MJ.Akka.EventReactor.Simple;

namespace MJ.Akka.EventReactor.Tests.TestData;

public class SimpleTestReactor(
    IImmutableList<(Events.IEvent evnt, IImmutableDictionary<string, object?> metadata)> events, 
    string? name = null) : SimpleEventReactor, ITestReactor
{
    private readonly ConcurrentDictionary<string, int> _handledEvents = [];
    private readonly EventSource _eventSource = new(events);

    public override string Name => !string.IsNullOrEmpty(name) ? name : GetType().Name;

    protected override ISetupSimpleEventReactor Configure(ISetupSimpleEventReactor config)
    {
        return ConfigureHandlers(config, _handledEvents);
    }

    public override Task<IEventReactorEventSource> GetSource()
    {
        return Task.FromResult<IEventReactorEventSource>(_eventSource);
    }
    
    public static ISetupSimpleEventReactor ConfigureHandlers(
        ISetupSimpleEventReactor config,
        ConcurrentDictionary<string, int> handledEvents)
    {
        var onceFailedEvents = new ConcurrentBag<string>();
        
        return config
            .On<Events.HandledEvent>()
            .ReactWith(evnt => handledEvents
                .AddOrUpdate(evnt.EventId, _ => 1, (_, current) => current + 1))
            .On<Events.EventThatFails>()
            .ReactWith(evnt => throw evnt.Exception)
            .On<Events.EventThatFailsOnce>()
            .ReactWith(evnt =>
            {
                if (!onceFailedEvents.Contains(evnt.EventId))
                {
                    onceFailedEvents.Add(evnt.EventId);

                    throw evnt.Exception;
                }

                handledEvents
                    .AddOrUpdate(evnt.EventId, _ => 1, (_, current) => current + 1);
            })
            .On<Events.TransformInto>()
            .ReactWith(evnt => handledEvents
                .AddOrUpdate(evnt.EventId, _ => 1, (_, current) => current + 1))
            .TransformWith(evnt => evnt.Results);
    }
    
    public IImmutableDictionary<string, int> GetHandledEvents()
    {
        return _handledEvents.ToImmutableDictionary();
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