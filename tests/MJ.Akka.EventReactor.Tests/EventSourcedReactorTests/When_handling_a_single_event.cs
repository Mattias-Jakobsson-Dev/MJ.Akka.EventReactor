using System.Collections.Immutable;
using Akka.Streams;
using Akka.Streams.Dsl;
using MJ.Akka.EventReactor.Tests.TestData;
using Xunit;

namespace MJ.Akka.EventReactor.Tests.EventSourcedReactorTests;

public class When_handling_a_single_event : PersistenceFixture
{
    private EventSourcedTestReactor _reactor = null!;
    private IImmutableList<object> _results = ImmutableList<object>.Empty;

    protected override Task Setup()
    {
        var events = ImmutableList.Create<(Events.IEvent evnt, IImmutableDictionary<string, object?> metadata)>(
            (new Events.HandledEvent("entity-1", "event-1"), ImmutableDictionary<string, object?>.Empty)
        );

        _reactor = new EventSourcedTestReactor(Sys, events);
        return Task.CompletedTask;
    }

    protected override async Task Run()
    {
        var source = await _reactor.GetSource();
        var reactor = _reactor.SetupReactor();

        var sink = Sink.ForEach<IMessageWithAck>(async msg =>
        {
            var results = await reactor.Handle(msg, CancellationToken.None);
            _results = _results.AddRange(results);
            await msg.Ack(CancellationToken.None);
        });

        await source.Start(CancellationToken.None).RunWith(sink, Sys.Materializer());
        
        await Task.Delay(100);
    }

    [Fact]
    public void Should_return_results()
    {
        Assert.NotEmpty(_results);
    }

    [Fact]
    public void Should_track_handled_event()
    {
        var handledEvents = _reactor.GetHandledEvents();
        Assert.NotEmpty(handledEvents);
        Assert.Contains("event-1", handledEvents.Keys);
        Assert.Equal(1, handledEvents["event-1"]);
    }
}

public class When_handling_event_that_persists_state : PersistenceFixture
{
    private EventSourcedTestReactor _reactor = null!;

    protected override Task Setup()
    {
        var events = ImmutableList.Create<(Events.IEvent evnt, IImmutableDictionary<string, object?> metadata)>(
            (new EventSourcedEvents.EventThatChangesState("entity-2", "event-2", "NewName"),
                ImmutableDictionary<string, object?>.Empty)
        );

        _reactor = new EventSourcedTestReactor(Sys, events);
        return Task.CompletedTask;
    }

    protected override async Task Run()
    {
        var source = await _reactor.GetSource();
        var reactor = _reactor.SetupReactor();

        var sink = Sink.ForEach<IMessageWithAck>(async msg =>
        {
            await reactor.Handle(msg, CancellationToken.None);
            await msg.Ack(CancellationToken.None);
        });

        await source.Start(CancellationToken.None).RunWith(sink, Sys.Materializer());
        
        await Task.Delay(100);
    }

    [Fact]
    public void Should_track_handled_event()
    {
        var handledEvents = _reactor.GetHandledEvents();
        Assert.NotEmpty(handledEvents);
        Assert.Contains("event-2", handledEvents.Keys);
        Assert.Equal(1, handledEvents["event-2"]);
    }

    [Fact]
    public void Should_persist_events_to_journal()
    {
        Assert.NotEmpty(StoredEvents);

        var persistedEvents = StoredEvents
            .Where(e => e.Event is EventSourcedEvents.EventThatChangesState)
            .ToList();

        Assert.Single(persistedEvents);
        Assert.IsType<EventSourcedEvents.EventThatChangesState>(persistedEvents[0].Event);
    }
}

public class When_handling_multiple_events_to_same_entity : PersistenceFixture
{
    private EventSourcedTestReactor _reactor = null!;

    protected override Task Setup()
    {
        var events = ImmutableList.Create<(Events.IEvent evnt, IImmutableDictionary<string, object?> metadata)>(
            (new EventSourcedEvents.EventThatChangesState("entity-3", "event-3a", "FirstName"),
                ImmutableDictionary<string, object?>.Empty),
            (new EventSourcedEvents.EventThatChangesState("entity-3", "event-3b", "SecondName"),
                ImmutableDictionary<string, object?>.Empty)
        );

        _reactor = new EventSourcedTestReactor(Sys, events);
        return Task.CompletedTask;
    }

    protected override async Task Run()
    {
        var source = await _reactor.GetSource();
        var reactor = _reactor.SetupReactor();

        var sink = Sink.ForEach<IMessageWithAck>(async msg =>
        {
            await reactor.Handle(msg, CancellationToken.None);
            await msg.Ack(CancellationToken.None);
        });

        await source.Start(CancellationToken.None).RunWith(sink, Sys.Materializer());
        
        await Task.Delay(100);
    }

    [Fact]
    public void Should_handle_both_events()
    {
        var handledEvents = _reactor.GetHandledEvents();
        Assert.Equal(2, handledEvents.Count);
        Assert.Contains("event-3a", handledEvents.Keys);
        Assert.Contains("event-3b", handledEvents.Keys);
    }

    [Fact]
    public void Should_persist_both_events()
    {
        var persistedEvents = StoredEvents
            .Where(e => e.Event is EventSourcedEvents.EventThatChangesState)
            .ToList();

        Assert.Equal(2, persistedEvents.Count);
    }
}

public class When_handling_unhandled_event : PersistenceFixture
{
    private EventSourcedTestReactor _reactor = null!;

    protected override Task Setup()
    {
        var events = ImmutableList.Create<(Events.IEvent evnt, IImmutableDictionary<string, object?> metadata)>(
            (new Events.UnHandledEvent("entity-4", "event-4"), ImmutableDictionary<string, object?>.Empty)
        );

        _reactor = new EventSourcedTestReactor(Sys, events);
        return Task.CompletedTask;
    }

    protected override async Task Run()
    {
        var source = await _reactor.GetSource();
        var reactor = _reactor.SetupReactor();

        var sink = Sink.ForEach<IMessageWithAck>(async msg =>
        {
            await reactor.Handle(msg, CancellationToken.None);
            await msg.Ack(CancellationToken.None);
        });

        await source.Start(CancellationToken.None).RunWith(sink, Sys.Materializer());
        
        await Task.Delay(100);
    }

    [Fact]
    public void Should_not_persist_anything()
    {
        Assert.Empty(StoredEvents.Where(e => e.Event is Events.UnHandledEvent));
    }
}










