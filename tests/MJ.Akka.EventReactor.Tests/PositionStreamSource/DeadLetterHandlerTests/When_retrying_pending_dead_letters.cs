using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using FluentAssertions;
using MJ.Akka.EventReactor.PositionStreamSource;
using Xunit;

namespace MJ.Akka.EventReactor.Tests.PositionStreamSource.DeadLetterHandlerTests;

public class When_retrying_pending_dead_letters(When_retrying_pending_dead_letters.Fixture fixture)
    : IClassFixture<When_retrying_pending_dead_letters.Fixture>
{
    [Fact]
    public void Then_parent_should_receive_push_dead_letter_messages()
    {
        fixture.PushedDeadLetters.Should().HaveCount(2);
    }

    [Fact]
    public void Then_pushed_dead_letters_should_have_correct_positions()
    {
        fixture.PushedDeadLetters.Select(x => x.OriginalPosition)
            .Should().BeEquivalentTo(1L, 2L);
    }

    [Fact]
    public void Then_no_new_events_should_be_stored()
    {
        // RetryPending does not persist anything itself
        fixture.StoredEvents.Should().BeEmpty();
    }

    public class Fixture : PersistenceFixture
    {
        public readonly string EventReactorName = Guid.NewGuid().ToString();
        private IActorRef _parent = null!;

        public IImmutableList<PositionedStreamPublisher.Commands.PushDeadLetter> PushedDeadLetters { get; private set; }
            = ImmutableList<PositionedStreamPublisher.Commands.PushDeadLetter>.Empty;

        protected override async Task Setup()
        {
            // Pre-seed a RetryStarted so there are active retries
            await WithEvents(
                $"event-reactor-dead-letters-{EventReactorName}",
                ImmutableList.Create<object>(
                    new DeadLetterHandler.Events.DeadLetterAdded(
                        EventReactorName, "test event 1", null, "Error", null, 1),
                    new DeadLetterHandler.Events.DeadLetterAdded(
                        EventReactorName, "test event 2", null, "Error", null, 2),
                    new DeadLetterHandler.Events.RetryStarted(EventReactorName, 1, 2)));

            _parent = Sys.ActorOf(Props.Create(() => new ParentActor(
                DeadLetterHandler.Init(EventReactorName),
                ActorRefs.Nobody)));
        }

        protected override async Task Run()
        {
            var dlq = await _parent.Ask<IActorRef>(new ParentActor.GetChild());

            // Allow actor to recover state
            await Task.Delay(300);

            dlq.Tell(new DeadLetterHandler.Commands.RetryPending());

            await Task.Delay(200);

            PushedDeadLetters = await _parent.Ask<IImmutableList<PositionedStreamPublisher.Commands.PushDeadLetter>>(
                new ParentActor.GetCollectedMessages());
        }
    }
}

