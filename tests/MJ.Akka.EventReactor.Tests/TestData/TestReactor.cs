using System.Collections.Concurrent;
using System.Collections.Immutable;
using Akka;
using Akka.Streams.Dsl;
using MJ.Akka.EventReactor.Setup;

namespace MJ.Akka.EventReactor.Tests.TestData;

public class TestReactor(
    IImmutableList<(Events.IEvent evnt, IImmutableDictionary<string, object?> metadata)> events, 
    string? name = null) : ITestReactor
{
    private readonly ConcurrentDictionary<string, int> _handledEvents = [];
    private readonly EventSource _eventSource = new(events);

    public string Name => !string.IsNullOrEmpty(name) ? name : GetType().Name;

    public ISetupEventReactor Configure(ISetupEventReactor config)
    {
        return ConfigureHandlers(config, _handledEvents);
    }

    public Task<IEventReactorEventSource> GetSource()
    {
        return Task.FromResult<IEventReactorEventSource>(_eventSource);
    }
    
    public static ISetupEventReactor ConfigureHandlers(
        ISetupEventReactor config,
        ConcurrentDictionary<string, int> handledEvents)
    {
        return config
            .On<Events.HandledEvent>()
            .ReactWith(evnt => handledEvents
                .AddOrUpdate(evnt.EventId, _ => 1, (_, current) => current + 1))
            .On<Events.EventThatFails>()
            .ReactWith(evnt => throw evnt.Exception)
            .On<Events.TransformInto>()
            .ReactWith(evnt => handledEvents
                .AddOrUpdate(evnt.EventId, _ => 1, (_, current) => current + 1))
            .TransformWith(evnt => evnt.Results);
    }
    
    public Task<IImmutableList<string>> GetDeadLetters()
    {
        return Task.FromResult(_eventSource.GetDeadLetters());
    }

    public IImmutableDictionary<string, int> GetHandledEvents()
    {
        return _handledEvents.ToImmutableDictionary();
    }

    private class EventSource(
        IImmutableList<(Events.IEvent evnt, IImmutableDictionary<string, object?> metadata)> events) : IEventReactorEventSource
    {
        private readonly ConcurrentBag<string> _deadLetters = [];
        private readonly ConcurrentBag<string> _eventsToSkip = [];
        
        public Source<IMessageWithAck, NotUsed> Start()
        {
            return Source.From(events.Where(x => !_eventsToSkip.Contains(x.evnt.EventId)))
                .Select(IMessageWithAck (x) => 
                    new EventWithAck(x.evnt, x.metadata, _eventsToSkip, _deadLetters));
        }
        
        public IImmutableList<string> GetDeadLetters()
        {
            return _deadLetters.ToImmutableList();
        }
    }
    
    private class EventWithAck(
        Events.IEvent evnt,
        IImmutableDictionary<string, object?> metadata,
        ConcurrentBag<string> eventsToSkip,
        ConcurrentBag<string> deadLetters) : IMessageWithAck
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
            deadLetters.Add(evnt.EventId);
            
            return Task.CompletedTask;
        }
    }
}