using System;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Akka.Actor;
using FluentAssertions;
using MJ.Akka.EventReactor.PositionStreamSource;
using Xunit;

namespace MJ.Akka.EventReactor.Tests.PositionStreamSource.DeadLetterHandlerTests;

public class When_acking_a_retried_dead_letter(When_acking_a_retried_dead_letter.Fixture fixture)
    : IClassFixture<When_acking_a_retried_dead_letter.Fixture>
{
    [Fact]
    public void Then_retry_started_and_ack_events_should_be_stored()
    {
        fixture.StoredEvents.Should().HaveCount(2);
    }

    [Fact]
    public void Then_retry_started_event_should_be_stored()
    {
        fixture.StoredEvents.Should().HaveEvents<DeadLetterHandler.Events.RetryStarted>(
            predicate: e => e.ReactorName == fixture.EventReactorName);
    }

    [Fact]
    public void Then_dead_letter_retried_successfully_event_should_be_stored()
    {
        fixture.StoredEvents.Should().HaveEvents<DeadLetterHandler.Events.DeadLetterRetriedSuccessfully>(
            predicate: e => e.ReactorName == fixture.EventReactorName && e.Position == 1);
    }

    public class Fixture : PersistenceFixture
    {
        public readonly string EventReactorName = Guid.NewGuid().ToString();
        private IActorRef _parent = null!;

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

            _parent = Sys.ActorOf(Props.Create(() => new ParentActor(
                DeadLetterHandler.Init(EventReactorName),
                ActorRefs.Nobody)));
        }

        protected override async Task Run()
        {
            var dlq = await _parent.Ask<IActorRef>(new ParentActor.GetChild());

            await dlq.Ask<DeadLetterHandler.Responses.ManageDeadLettersResponse>(
                new DeadLetterHandler.Commands.RetryDeadLetters(1));

            dlq.Tell(new DeadLetterHandler.Commands.AckRetry(1));

            // Small wait for the persist to complete
            await Task.Delay(200);
        }
    }
}

