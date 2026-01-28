using System.Collections.Immutable;
using Akka.Actor;
using FluentAssertions;
using MJ.Akka.EventReactor.PositionStreamSource;
using Xunit;

namespace MJ.Akka.EventReactor.Tests.PositionStreamSource.DeadLetterHandlerTests;

public class When_clearing_dlq_with_single_message(When_clearing_dlq_with_single_message.Fixture fixture) 
    : IClassFixture<When_clearing_dlq_with_single_message.Fixture>
{
    [Fact]
    public void Then_one_event_should_be_stored()
    {
        fixture.StoredEvents.Should().HaveCount(1);
    }
    
    [Fact]
    public void Then_stored_event_should_be_dead_letters_cleared_event()
    {
        fixture.StoredEvents.Should().HaveEvents<DeadLetterHandler.Events.DeadLettersCleared>(
            predicate: e => e.ReactorName == fixture.EventReactorName &&
                            e.Position == 1);
    }
    
    [Fact]
    public void Then_there_should_be_one_remaining_event()
    {
        fixture.RemainingEvents.Should().HaveCount(1);
    }

    [Fact]
    public void Then_remaining_event_should_be_dead_letters_cleared_event()
    {
        fixture.RemainingEvents.Should().HaveEvents<DeadLetterHandler.Events.DeadLettersCleared>(
            predicate: e => e.ReactorName == fixture.EventReactorName &&
                            e.Position == 1);
    }
    
    public class Fixture : PersistenceFixture
    {
        public readonly string EventReactorName = Guid.NewGuid().ToString();
        private IActorRef _dlq = null!;
        
        public IImmutableList<StoredEventsInterceptor.StoredEvent> RemainingEvents { get; private set; } =
            ImmutableList<StoredEventsInterceptor.StoredEvent>.Empty;
        
        protected override async Task Setup()
        {
            await WithEvents(
                $"event-reactor-dead-letters-{EventReactorName}",
                ImmutableList.Create<object>(
                    new DeadLetterHandler.Events.DeadLetterAdded(
                        EventReactorName,
                        "test event",
                        null,
                        "Test error",
                        null,
                        1)));

            _dlq = Sys.ActorOf(DeadLetterHandler.Init(EventReactorName));
        }

        protected override async Task Run()
        {
            await _dlq.Ask<DeadLetterHandler.Responses.ManageDeadLettersResponse>(
                new DeadLetterHandler.Commands.ClearDeadLetters(long.MaxValue));

            RemainingEvents = await ReadEvents($"event-reactor-dead-letters-{EventReactorName}");
        }
    }
}