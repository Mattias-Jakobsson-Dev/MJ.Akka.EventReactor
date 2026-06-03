using System.Collections.Concurrent;
using System.Collections.Immutable;
using Akka;
using Akka.Streams.Dsl;

namespace MJ.Akka.EventReactor.TestKit;

public class TestEventReactor(IConfigureEventReactor reactorToTest, params object[] events)
    : IConfigureEventReactor
{
    private readonly ConcurrentBag<object> _ackedMessages = [];
    private readonly ConcurrentBag<(object message, Exception exception)> _nackedMessages = [];

    public IImmutableList<object> AckedMessages => _ackedMessages.ToImmutableList();
    public IImmutableList<(object message, Exception exception)> NackedMessages => _nackedMessages.ToImmutableList();

    public string Name => reactorToTest.Name;

    public Task<IEventReactorEventSource> GetSource()
    {
        return Task.FromResult<IEventReactorEventSource>(
            new EventSource(events.ToImmutableList(), _ackedMessages, _nackedMessages));
    }

    public IReactToEvent SetupReactor()
    {
        return reactorToTest.SetupReactor();
    }

    private class EventSource(
        IImmutableList<object> events,
        ConcurrentBag<object> ackedMessages,
        ConcurrentBag<(object, Exception)> nackedMessages) : IEventReactorEventSource
    {
        public Source<IMessageWithAck, NotUsed> Start(CancellationToken cancellationToken)
        {
            return Source.FromEnumerator(() => events
                .Select(IMessageWithAck (evnt) => new TestMessageWithAck(
                    evnt, 
                    ImmutableDictionary<string, object?>.Empty, 
                    ackedMessages, 
                    nackedMessages))
                .GetEnumerator());
        }
    }

    private record TestMessageWithAck(
        object Message,
        IImmutableDictionary<string, object?> Metadata,
        ConcurrentBag<object> AckedMessages,
        ConcurrentBag<(object, Exception)> NackedMessages) : IMessageWithAck
    {
        public Task Ack(CancellationToken cancellationToken)
        {
            AckedMessages.Add(Message);

            return Task.CompletedTask;
        }

        public Task Nack(Exception error, CancellationToken cancellationToken)
        {
            NackedMessages.Add((Message, error));

            return Task.CompletedTask;
        }
    }
}