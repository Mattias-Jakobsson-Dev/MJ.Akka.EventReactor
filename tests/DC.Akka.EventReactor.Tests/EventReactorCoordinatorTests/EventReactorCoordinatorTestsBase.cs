using System.Collections.Immutable;
using DC.Akka.EventReactor.Setup;
using DC.Akka.EventReactor.Tests.TestData;
using FluentAssertions;
using Xunit;

namespace DC.Akka.EventReactor.Tests.EventReactorCoordinatorTests;

public abstract class EventReactorCoordinatorTestsBase(IHaveActorSystem actorSystemHandler)
{
    [Fact]
    public async Task Reacting_to_event_that_is_successful()
    {
        using var system = actorSystemHandler.StartNewActorSystem();

        var eventId = Guid.NewGuid().ToString();

        var reactor = CreateReactor(ImmutableList.Create<Events.IEvent>(new Events.HandledEvent(eventId)));

        var coordinator = await system
            .EventReactors(config => config
                .WithReactor(reactor, Configure))
            .Start();

        await coordinator.Get(reactor.Name)!.WaitForCompletion(TimeSpan.FromSeconds(5));

        reactor.GetHandledEvents().Keys.Should().BeEquivalentTo(ImmutableList.Create(eventId));
        reactor.GetHandledEvents()[eventId].Should().Be(1);
        (await reactor.GetDeadLetters(system)).Should().BeEmpty();
    }

    [Fact]
    public async Task Reacting_to_event_that_fails()
    {
        using var system = actorSystemHandler.StartNewActorSystem();

        var eventId = Guid.NewGuid().ToString();

        var reactor = new TestReactor(ImmutableList.Create<Events.IEvent>(
            new Events.EventThatFails(eventId, new Exception("Failed"))));

        var coordinator = await system
            .EventReactors(config => config
                .WithReactor(reactor, Configure))
            .Start();

        await coordinator.Get(reactor.Name)!.WaitForCompletion(TimeSpan.FromSeconds(5));

        reactor.GetHandledEvents().Should().HaveCount(0);
        (await reactor.GetDeadLetters(system)).Should().BeEquivalentTo(ImmutableList.Create(eventId));
    }

    [Fact]
    public async Task Reacting_to_event_that_is_successful_and_then_running_again()
    {
        using var system = actorSystemHandler.StartNewActorSystem();

        var eventId = Guid.NewGuid().ToString();

        var reactor = CreateReactor(ImmutableList.Create<Events.IEvent>(new Events.HandledEvent(eventId)));

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
        (await reactor.GetDeadLetters(system)).Should().BeEmpty();
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
            .Select(x => random.Next(0, 100) < failurePercentage
                ? (Events.IEvent)new Events.EventThatFails(Guid.NewGuid().ToString(), new Exception("Failed"))
                : new Events.HandledEvent(Guid.NewGuid().ToString()))
            .ToImmutableList();

        var reactor = CreateReactor(events);

        var coordinator = await system
            .EventReactors(config => config
                .WithReactor(reactor, Configure))
            .Start();

        await coordinator.Get(reactor.Name)!.WaitForCompletion(TimeSpan.FromSeconds(5));

        var successfulEvents = events
            .Where(x => x is Events.HandledEvent)
            .Select(x => x.EventId)
            .ToImmutableList();
        
        var failureEvents = events
            .Where(x => x is Events.EventThatFails)
            .Select(x => x.EventId)
            .ToImmutableList();

        reactor.GetHandledEvents().Keys.Should().BeEquivalentTo(successfulEvents);

        foreach (var successfulEvent in successfulEvents)
            reactor.GetHandledEvents()[successfulEvent].Should().Be(1);

        (await reactor.GetDeadLetters(system)).Should().BeEquivalentTo(failureEvents);
    }

    protected virtual IHaveConfiguration<EventReactorInstanceConfig> Configure(
        IHaveConfiguration<EventReactorInstanceConfig> config)
    {
        return config;
    }

    protected abstract ITestReactor CreateReactor(IImmutableList<Events.IEvent> events);
}