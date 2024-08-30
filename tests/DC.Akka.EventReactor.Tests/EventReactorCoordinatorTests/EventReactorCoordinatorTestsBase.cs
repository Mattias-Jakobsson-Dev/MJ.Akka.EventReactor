using System;
using System.Collections.Immutable;
using System.Threading.Tasks;
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
    }

    protected virtual IHaveConfiguration<EventReactorInstanceConfig> Configure(
        IHaveConfiguration<EventReactorInstanceConfig> config)
    {
        return config;
    }
    
    protected abstract ITestReactor CreateReactor(IImmutableList<Events.IEvent> events);
}