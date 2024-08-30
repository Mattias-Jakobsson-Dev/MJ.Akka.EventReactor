using System.Collections.Concurrent;
using System.Collections.Immutable;
using Akka;
using Akka.Streams.Dsl;

namespace DC.Akka.EventReactor.Tests.TestData;

public class TestReactor(IImmutableList<Events.IEvent> events) : ITestReactor
{
    private readonly ConcurrentDictionary<string, int> _handledEvents = [];

    public string Name => GetType().Name;

    public ISetupEventReactor Configure(ISetupEventReactor config)
    {
        return ConfigureHandlers(config, _handledEvents);
    }

    public Source<IMessageWithAck, NotUsed> StartSource()
    {
        return Source.From(events)
            .Select(IMessageWithAck (x) => new EventWithAck(x));
    }

    public static ISetupEventReactor ConfigureHandlers(
        ISetupEventReactor config,
        ConcurrentDictionary<string, int> handledEvents)
    {
        return config
            .On<Events.HandledEvent>(evnt => handledEvents
                .AddOrUpdate(evnt.EventId, _ => 1, (_, current) => current + 1))
            .On<Events.EventThatFails>(evnt => throw evnt.Exception);
    }

    private class EventWithAck(Events.IEvent evnt) : IMessageWithAck
    {
        public object Message { get; } = evnt;

        public Task Ack()
        {
            return Task.CompletedTask;
        }

        public Task Nack(Exception error)
        {
            return Task.CompletedTask;
        }
    }

    public IImmutableDictionary<string, int> GetHandledEvents()
    {
        return _handledEvents.ToImmutableDictionary();
    }
}