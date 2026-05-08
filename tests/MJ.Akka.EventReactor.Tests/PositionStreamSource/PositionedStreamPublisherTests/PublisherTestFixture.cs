using System.Collections.Immutable;
using Akka;
using Akka.Actor;
using Akka.Streams.Dsl;
using Akka.TestKit;
using MJ.Akka.EventReactor.PositionStreamSource;

namespace MJ.Akka.EventReactor.Tests.PositionStreamSource.PositionedStreamPublisherTests;

/// <summary>
/// An IStartPositionStream that emits a fixed list of events and then completes.
/// </summary>
public class FixedEventPositionStream(IReadOnlyList<EventWithPosition> events) : IStartPositionStream
{
    public Source<EventWithPosition, NotUsed> StartFrom(long? position) =>
        Source.From(events);

    public Task<long?> GetInitialPosition() => Task.FromResult<long?>(null);
}

public abstract class PublisherTestFixture : PersistenceFixture
{
    protected IActorRef Publisher { get; private set; } = null!;
    protected TestProbe WorkerProbe { get; private set; } = null!;

    protected abstract PositionedStreamSettings Settings { get; }
    protected abstract IReadOnlyList<EventWithPosition> Events { get; }

    protected override Task Setup()
    {
        WorkerProbe = CreateTestProbe();

        var name = Guid.NewGuid().ToString();

        Publisher = Sys.ActorOf(
            Props.Create(() => new PositionedStreamPublisher(
                name,
                new FixedEventPositionStream(Events),
                Settings)),
            $"publisher-{name}");

        return Task.CompletedTask;
    }

    protected static EventWithPosition MakeEvent(long position, string payload = "test-event") =>
        new(payload, ImmutableDictionary<string, object?>.Empty, position);
}
