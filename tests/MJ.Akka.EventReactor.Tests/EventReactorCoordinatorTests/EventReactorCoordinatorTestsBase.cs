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
                (new Events.HandledEvent(eventId),
                    new Dictionary<string, object?>().ToImmutableDictionary())),
            system);

        var coordinator = await system
            .EventReactors(config => config
                .WithReactor(reactor, Configure))
            .Start();

        await coordinator.Get(reactor.Name)!.WaitForCompletion(TimeSpan.FromSeconds(5));

        reactor.GetHandledEvents().Keys.Should().BeEquivalentTo(ImmutableList.Create(eventId));
        reactor.GetHandledEvents()[eventId].Should().Be(1);
        (await reactor.GetDeadLetters()).Should().BeEmpty();
    }

    [Fact]
    public async Task Reacting_to_event_that_is_successful_on_two_reactors_at_once()
    {
        using var system = actorSystemHandler.StartNewActorSystem();
        
        var firstEventId = Guid.NewGuid().ToString();

        var firstReactor = CreateReactor(
            ImmutableList.Create<(Events.IEvent, IImmutableDictionary<string, object?>)>(
                (new Events.HandledEvent(firstEventId),
                    new Dictionary<string, object?>().ToImmutableDictionary())),
            system,
            "first-reactor");
        
        var secondEventId = Guid.NewGuid().ToString();

        var secondReactor = CreateReactor(
            ImmutableList.Create<(Events.IEvent, IImmutableDictionary<string, object?>)>(
                (new Events.HandledEvent(secondEventId),
                    new Dictionary<string, object?>().ToImmutableDictionary())),
            system,
            "second-reactor");

        var coordinator = await system
            .EventReactors(config => config
                .WithReactor(firstReactor, Configure)
                .WithReactor(secondReactor, Configure))
            .Start();

        await coordinator.Get(firstReactor.Name)!.WaitForCompletion(TimeSpan.FromSeconds(5));
        await coordinator.Get(secondReactor.Name)!.WaitForCompletion(TimeSpan.FromSeconds(5));

        firstReactor.GetHandledEvents().Keys.Should().BeEquivalentTo(ImmutableList.Create(firstEventId));
        firstReactor.GetHandledEvents()[firstEventId].Should().Be(1);
        (await firstReactor.GetDeadLetters()).Should().BeEmpty();
        
        secondReactor.GetHandledEvents().Keys.Should().BeEquivalentTo(ImmutableList.Create(secondEventId));
        secondReactor.GetHandledEvents()[secondEventId].Should().Be(1);
        (await secondReactor.GetDeadLetters()).Should().BeEmpty();
    }

    [Fact]
    public async Task Reacting_to_event_that_fails()
    {
        using var system = actorSystemHandler.StartNewActorSystem();

        var eventId = Guid.NewGuid().ToString();

        var reactor = new TestReactor(ImmutableList.Create<(Events.IEvent, IImmutableDictionary<string, object?>)>(
            (new Events.EventThatFails(eventId, new Exception("Failed")),
                new Dictionary<string, object?>().ToImmutableDictionary())));

        var coordinator = await system
            .EventReactors(config => config
                .WithReactor(reactor, Configure))
            .Start();

        await coordinator.Get(reactor.Name)!.WaitForCompletion(TimeSpan.FromSeconds(5));

        reactor.GetHandledEvents().Should().HaveCount(0);
        (await reactor.GetDeadLetters()).Should().BeEquivalentTo(ImmutableList.Create(eventId));
    }

    [Fact]
    public async Task Reacting_to_event_that_is_successful_and_then_running_again()
    {
        using var system = actorSystemHandler.StartNewActorSystem();

        var eventId = Guid.NewGuid().ToString();

        var reactor = CreateReactor(
            ImmutableList.Create<(Events.IEvent, IImmutableDictionary<string, object?>)>(
                (new Events.HandledEvent(eventId),
                    new Dictionary<string, object?>().ToImmutableDictionary())),
            system);

        var firstCoordinator = await system
            .EventReactors(config => config
                .WithReactor(reactor, Configure))
            .Start();

        await firstCoordinator.Get(reactor.Name)!.WaitForCompletion(TimeSpan.FromSeconds(5));

        var secondCoordinator = await system
            .EventReactors(config => config
                .WithReactor(reactor, Configure))
            .Start();

        await secondCoordinator.Get(reactor.Name)!.WaitForCompletion(TimeSpan.FromSeconds(5));

        reactor.GetHandledEvents().Keys.Should().BeEquivalentTo(ImmutableList.Create(eventId));
        reactor.GetHandledEvents()[eventId].Should().Be(1);
        (await reactor.GetDeadLetters()).Should().BeEmpty();
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
                ? (Events.IEvent)new Events.EventThatFails(Guid.NewGuid().ToString(), new Exception("Failed"))
                : new Events.HandledEvent(Guid.NewGuid().ToString()),
                metadata: (IImmutableDictionary<string, object?>)new Dictionary<string, object?>().ToImmutableDictionary()))
            .ToImmutableList();

        var reactor = CreateReactor(events, system);

        var coordinator = await system
            .EventReactors(config => config
                .WithReactor(reactor, Configure))
            .Start();

        await coordinator.Get(reactor.Name)!.WaitForCompletion(TimeSpan.FromSeconds(5));

        var successfulEvents = events
            .Where(x => x.evnt is Events.HandledEvent)
            .Select(x => x.evnt.EventId)
            .ToImmutableList();
        
        var failureEvents = events
            .Where(x => x.evnt is Events.EventThatFails)
            .Select(x => x.evnt.EventId)
            .ToImmutableList();

        reactor.GetHandledEvents().Keys.Should().BeEquivalentTo(successfulEvents);

        foreach (var successfulEvent in successfulEvents)
            reactor.GetHandledEvents()[successfulEvent].Should().Be(1);

        (await reactor.GetDeadLetters()).Should().BeEquivalentTo(failureEvents);
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

        await coordinator.Get(reactor.Name)!.WaitForCompletion(TimeSpan.FromSeconds(5));

        reactor.GetHandledEvents().Keys.Should().BeEquivalentTo(ImmutableList.Create(eventId));
        reactor.GetHandledEvents()[eventId].Should().Be(1);
        (await reactor.GetDeadLetters()).Should().BeEmpty();

        var transformedEvents = outputWriter.GetItems();

        transformedEvents.Should().HaveCount(2);
        transformedEvents.Should().Contain(firstTransformedTo);
        transformedEvents.Should().Contain(secondTransformedTo);
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