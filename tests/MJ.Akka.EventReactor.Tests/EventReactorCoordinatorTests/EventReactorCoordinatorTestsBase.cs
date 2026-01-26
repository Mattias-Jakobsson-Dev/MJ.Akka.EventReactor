using System.Collections.Immutable;
using Akka.Actor;
using MJ.Akka.EventReactor.Setup;
using FluentAssertions;
using MJ.Akka.EventReactor.Configuration;
using MJ.Akka.EventReactor.Tests.TestData;
using Xunit;

namespace MJ.Akka.EventReactor.Tests.EventReactorCoordinatorTests;

public abstract class EventReactorCoordinatorTestsBase(IHaveActorSystem actorSystemHandler)
{
    [Fact]
    public async Task Reacting_to_event_that_is_successful()
    {
        using var system = actorSystemHandler.StartNewActorSystem();

        var eventId = Guid.NewGuid().ToString();

        var reactor = CreateReactor(
            ImmutableList.Create<(Events.IEvent, IImmutableDictionary<string, object?>)>(
                (new Events.HandledEvent(Guid.NewGuid().ToString(), eventId),
                    new Dictionary<string, object?>().ToImmutableDictionary())),
            system);

        var coordinator = await system
            .EventReactors(config => config
                .WithReactor(reactor, Configure))
            .Start();

        var reactorProxy = coordinator.Get(reactor.Name)!;

        await reactorProxy.WaitForCompletion(TimeSpan.FromSeconds(5));

        reactor.GetHandledEvents().Keys.Should().BeEquivalentTo(ImmutableList.Create(eventId));
        reactor.GetHandledEvents()[eventId].Should().Be(1);
    }

    [Fact]
    public async Task Reacting_to_event_that_is_successful_on_two_reactors_at_once()
    {
        using var system = actorSystemHandler.StartNewActorSystem();

        var firstEventId = Guid.NewGuid().ToString();

        var firstReactor = CreateReactor(
            ImmutableList.Create<(Events.IEvent, IImmutableDictionary<string, object?>)>(
                (new Events.HandledEvent(Guid.NewGuid().ToString(), firstEventId),
                    new Dictionary<string, object?>().ToImmutableDictionary())),
            system,
            "first-reactor");

        var secondEventId = Guid.NewGuid().ToString();

        var secondReactor = CreateReactor(
            ImmutableList.Create<(Events.IEvent, IImmutableDictionary<string, object?>)>(
                (new Events.HandledEvent(Guid.NewGuid().ToString(), secondEventId),
                    new Dictionary<string, object?>().ToImmutableDictionary())),
            system,
            "second-reactor");

        var coordinator = await system
            .EventReactors(config => config
                .WithReactor(firstReactor, Configure)
                .WithReactor(secondReactor, Configure))
            .Start();

        var firstReactorProxy = coordinator.Get(firstReactor.Name)!;
        var secondReactorProxy = coordinator.Get(secondReactor.Name)!;

        await firstReactorProxy.WaitForCompletion(TimeSpan.FromSeconds(5));
        await secondReactorProxy.WaitForCompletion(TimeSpan.FromSeconds(5));

        firstReactor.GetHandledEvents().Keys.Should().BeEquivalentTo(ImmutableList.Create(firstEventId));
        firstReactor.GetHandledEvents()[firstEventId].Should().Be(1);

        secondReactor.GetHandledEvents().Keys.Should().BeEquivalentTo(ImmutableList.Create(secondEventId));
        secondReactor.GetHandledEvents()[secondEventId].Should().Be(1);
    }

    [Fact]
    public async Task Reacting_to_event_that_fails()
    {
        using var system = actorSystemHandler.StartNewActorSystem();

        var eventId = Guid.NewGuid().ToString();

        var reactor = CreateReactor(ImmutableList.Create<(Events.IEvent, IImmutableDictionary<string, object?>)>(
                (new Events.EventThatFails(Guid.NewGuid().ToString(), eventId),
                    new Dictionary<string, object?>().ToImmutableDictionary())),
            system);

        var coordinator = await system
            .EventReactors(config => config
                .WithReactor(reactor, Configure))
            .Start();

        var reactorProxy = coordinator.Get(reactor.Name)!;

        await reactorProxy.WaitForCompletion(TimeSpan.FromSeconds(5));

        reactor.GetHandledEvents().Should().HaveCount(0);
    }

    [Fact]
    public async Task Reacting_to_event_that_fails_once_and_retrying_if_available()
    {
        using var system = actorSystemHandler.StartNewActorSystem();

        var eventId = Guid.NewGuid().ToString();

        var reactor = CreateReactor(ImmutableList.Create<(Events.IEvent, IImmutableDictionary<string, object?>)>(
                (new Events.EventThatFailsOnce(Guid.NewGuid().ToString(), eventId),
                    new Dictionary<string, object?>().ToImmutableDictionary())),
            system);

        var coordinator = await system
            .EventReactors(config => config
                .WithReactor(reactor, Configure))
            .Start();

        var reactorProxy = coordinator.Get(reactor.Name)!;

        await reactorProxy.WaitForCompletion(TimeSpan.FromSeconds(5));

        reactor.GetHandledEvents().Should().HaveCount(0);
    }

    [Fact]
    public async Task Reacting_to_event_that_is_successful_and_then_running_again()
    {
        using var system = actorSystemHandler.StartNewActorSystem();

        var eventId = Guid.NewGuid().ToString();

        var reactor = CreateReactor(
            ImmutableList.Create<(Events.IEvent, IImmutableDictionary<string, object?>)>(
                (new Events.HandledEvent(Guid.NewGuid().ToString(), eventId),
                    new Dictionary<string, object?>().ToImmutableDictionary())),
            system);

        var firstCoordinator = await system
            .EventReactors(config => config
                .WithReactor(reactor, Configure))
            .Start();

        var firstCoordinatorProxy = firstCoordinator.Get(reactor.Name)!;

        await firstCoordinatorProxy.WaitForCompletion(TimeSpan.FromSeconds(5));

        var secondCoordinator = await system
            .EventReactors(config => config
                .WithReactor(reactor, Configure))
            .Start();

        var secondCoordinatorProxy = secondCoordinator.Get(reactor.Name)!;

        await secondCoordinatorProxy.WaitForCompletion(TimeSpan.FromSeconds(5));

        reactor.GetHandledEvents().Keys.Should().BeEquivalentTo(ImmutableList.Create(eventId));
        reactor.GetHandledEvents()[eventId].Should().Be(1);
    }

    [Theory]
    [InlineData(100, 5)]
    [InlineData(100, 1)]
    [InlineData(100, 0)]
    public async Task Reacting_to_events_with_random_failures(int numberOfEvents, int failurePercentage)
    {
        var random = new Random();

        using var system = actorSystemHandler.StartNewActorSystem();

        var events = Enumerable.Range(0, numberOfEvents)
            .Select(_ => (evnt: random.Next(0, 100) < failurePercentage
                    ? (Events.IEvent)new Events.EventThatFails(Guid.NewGuid().ToString(), Guid.NewGuid().ToString())
                    : new Events.HandledEvent(Guid.NewGuid().ToString(), Guid.NewGuid().ToString()),
                metadata: (IImmutableDictionary<string, object?>)
                new Dictionary<string, object?>().ToImmutableDictionary()))
            .ToImmutableList();

        var reactor = CreateReactor(events, system);

        var coordinator = await system
            .EventReactors(config => config
                .WithReactor(reactor, Configure))
            .Start();

        var reactorProxy = coordinator.Get(reactor.Name)!;

        await reactorProxy.WaitForCompletion(TimeSpan.FromSeconds(5));

        var successfulEvents = events
            .Where(x => x.evnt is Events.HandledEvent)
            .Select(x => x.evnt.EventId)
            .ToImmutableList();

        reactor.GetHandledEvents().Keys.Should().BeEquivalentTo(successfulEvents);

        foreach (var successfulEvent in successfulEvents)
            reactor.GetHandledEvents()[successfulEvent].Should().Be(1);
    }

    [Fact]
    public async Task Reacting_to_single_transform_event()
    {
        using var system = actorSystemHandler.StartNewActorSystem();

        var eventId = Guid.NewGuid().ToString();

        var firstTransformedTo = Guid.NewGuid().ToString();
        var secondTransformedTo = Guid.NewGuid().ToString();

        var outputWriter = new TestOutputWriter();

        var reactor = CreateReactor(
            ImmutableList.Create<(Events.IEvent, IImmutableDictionary<string, object?>)>(
                (new Events.TransformInto(
                        Guid.NewGuid().ToString(),
                        eventId,
                        ImmutableList.Create<object>(
                            firstTransformedTo,
                            secondTransformedTo)),
                    new Dictionary<string, object?>().ToImmutableDictionary())),
            system);

        var coordinator = await system
            .EventReactors(config => config
                .WithReactor(reactor, Configure)
                .WithOutputWriter(outputWriter))
            .Start();

        var reactorProxy = coordinator.Get(reactor.Name)!;

        await reactorProxy.WaitForCompletion(TimeSpan.FromSeconds(5));

        reactor.GetHandledEvents().Keys.Should().BeEquivalentTo(ImmutableList.Create(eventId));
        reactor.GetHandledEvents()[eventId].Should().Be(1);
        
        var transformedEvents = outputWriter.GetItems();

        transformedEvents.Should().HaveCount(2);
        transformedEvents.Should().Contain(firstTransformedTo);
        transformedEvents.Should().Contain(secondTransformedTo);
    }

    [Fact]
    public async Task React_single_modify_state_event()
    {
        using var system = actorSystemHandler.StartNewActorSystem();

        var eventId = Guid.NewGuid().ToString();
        var entityId = Guid.NewGuid().ToString();

        var reactor = CreateReactor(
            ImmutableList.Create<(Events.IEvent, IImmutableDictionary<string, object?>)>(
                (new StatefulEvents.EventThatModifiesState(
                        entityId,
                        eventId,
                        _ => Task.FromResult<TestState?>(new TestState("Test"))),
                    new Dictionary<string, object?>().ToImmutableDictionary())),
            system);
        
        if (reactor is not IStatefulTestReactor statefulTestReactor)
            return;

        var coordinator = await system
            .EventReactors(config => config
                .WithReactor(reactor, Configure))
            .Start();

        var reactorProxy = coordinator.Get(reactor.Name)!;

        await reactorProxy.WaitForCompletion(TimeSpan.FromSeconds(5));

        reactor.GetHandledEvents().Keys.Should().BeEquivalentTo(ImmutableList.Create(eventId));
        reactor.GetHandledEvents()[eventId].Should().Be(1);

        var storage = statefulTestReactor.GetStorage();

        var state = await storage.Load<TestState>(reactor.Name, entityId, CancellationToken.None);

        state.Should().NotBeNull();
        state!.Name.Should().Be("Test");
    }

    [Fact]
    public async Task React_to_two_modify_state_events_for_same_id_where_first_one_is_slow()
    {
        using var system = actorSystemHandler.StartNewActorSystem();

        var firstEventId = Guid.NewGuid().ToString();
        var secondEventId = Guid.NewGuid().ToString();
        var entityId = Guid.NewGuid().ToString();

        var reactor = CreateReactor(
            ImmutableList.Create<(Events.IEvent, IImmutableDictionary<string, object?>)>(
                (new StatefulEvents.EventThatModifiesState(
                        entityId,
                        firstEventId,
                        async _ =>
                        {
                            await Task.Delay(TimeSpan.FromMilliseconds(500));

                            return new TestState("Test1");
                        }),
                    new Dictionary<string, object?>().ToImmutableDictionary()),
                (new StatefulEvents.EventThatModifiesState(
                        entityId,
                        secondEventId,
                        _ => Task.FromResult<TestState?>(new TestState("Test2"))),
                    new Dictionary<string, object?>().ToImmutableDictionary())),
            system);
        
        if (reactor is not IStatefulTestReactor statefulTestReactor)
            return;

        var coordinator = await system
            .EventReactors(config => config
                .WithReactor(reactor, Configure))
            .Start();

        var reactorProxy = coordinator.Get(reactor.Name)!;

        await reactorProxy.WaitForCompletion(TimeSpan.FromSeconds(5));

        reactor.GetHandledEvents().Keys.Should().BeEquivalentTo(ImmutableList.Create(firstEventId, secondEventId));
        reactor.GetHandledEvents()[firstEventId].Should().Be(1);
        reactor.GetHandledEvents()[secondEventId].Should().Be(1);

        var storage = statefulTestReactor.GetStorage();

        var state = await storage.Load<TestState>(reactor.Name, entityId, CancellationToken.None);

        state.Should().NotBeNull();
        state!.Name.Should().Be("Test2");
    }

    protected virtual IHaveConfiguration<EventReactorInstanceConfig> Configure(
        IHaveConfiguration<EventReactorInstanceConfig> config)
    {
        return config;
    }

    protected abstract ITestReactor CreateReactor(
        IImmutableList<(Events.IEvent, IImmutableDictionary<string, object?>)> events,
        ActorSystem actorSystem,
        string? name = null);
}