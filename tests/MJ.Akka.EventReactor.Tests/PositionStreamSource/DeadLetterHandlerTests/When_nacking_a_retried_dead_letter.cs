using System.Collections.Immutable;
using Akka.Actor;
using FluentAssertions;
using MJ.Akka.EventReactor.PositionStreamSource;
using Xunit;

namespace MJ.Akka.EventReactor.Tests.PositionStreamSource.DeadLetterHandlerTests;

public class When_nacking_a_retried_dead_letter(When_nacking_a_retried_dead_letter.Fixture fixture)
    : IClassFixture<When_nacking_a_retried_dead_letter.Fixture>
{
    [Fact]
    public void Then_retry_started_and_nack_events_should_be_stored()
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
    public void Then_dead_letter_added_event_should_be_stored_with_new_error()
    {
        fixture.StoredEvents.Should().HaveEvents<DeadLetterHandler.Events.DeadLetterAdded>(
            predicate: e => e.ReactorName == fixture.EventReactorName &&
                            e.ErrorMessage == "Retry failed");
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
                        "Original error",
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

            dlq.Tell(new DeadLetterHandler.Commands.NackRetry(1, new Exception("Retry failed")));

            await Task.Delay(200);
        }
    }
}

