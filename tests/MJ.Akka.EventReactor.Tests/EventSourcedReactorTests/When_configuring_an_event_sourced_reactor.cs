using System.Collections.Immutable;
using MJ.Akka.EventReactor.Tests.TestData;
using Xunit;

namespace MJ.Akka.EventReactor.Tests.EventSourcedReactorTests;

public class When_configuring_an_event_sourced_reactor : PersistenceFixture
{
    private EventSourcedTestReactor _reactor = null!;

    protected override Task Setup()
    {
        var events = ImmutableList.Create<(Events.IEvent evnt, IImmutableDictionary<string, object?> metadata)>(
            (new Events.HandledEvent("entity-1", "event-1"), ImmutableDictionary<string, object?>.Empty)
        );

        _reactor = new EventSourcedTestReactor(Sys, events);
        return Task.CompletedTask;
    }

    protected override Task Run()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public void Should_configure_reactor_successfully()
    {
        var reactor = _reactor.SetupReactor();
        Assert.NotNull(reactor);
    }

    [Fact]
    public void Should_have_a_name()
    {
        Assert.NotNull(_reactor.Name);
        Assert.NotEmpty(_reactor.Name);
    }

    [Fact]
    public async Task Should_have_an_event_source()
    {
        var source = await _reactor.GetSource();
        Assert.NotNull(source);
    }
}

public class When_setting_up_event_appliers : PersistenceFixture
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

    protected override Task Run()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public void Should_create_reactor_with_appliers()
    {
        var reactor = _reactor.SetupReactor();
        Assert.NotNull(reactor);
    }
}

public class When_handling_unhandled_events : PersistenceFixture
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

    protected override Task Run()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public void Should_handle_gracefully()
    {
        var reactor = _reactor.SetupReactor();
        Assert.NotNull(reactor);
    }
}


