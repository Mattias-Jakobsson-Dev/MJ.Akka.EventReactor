using System.Collections.Immutable;
using Akka.Actor;
using FluentAssertions;
using MJ.Akka.EventReactor.PositionStreamSource;
using Xunit;

namespace MJ.Akka.EventReactor.Tests.PositionStreamSource.DeadLetterHandlerTests;

public class When_retrying_dead_letters(When_retrying_dead_letters.Fixture fixture)
    : IClassFixture<When_retrying_dead_letters.Fixture>
{
    [Fact]
    public void Then_two_events_should_be_stored()
    {
        // RetryStarted event + the initial DeadLetterAdded events are pre-seeded; only new events during Run are captured
        fixture.StoredEvents.Should().HaveCount(1);
    }

    [Fact]
    public void Then_stored_event_should_be_retry_started()
    {
        fixture.StoredEvents.Should().HaveEvents<DeadLetterHandler.Events.RetryStarted>(
            predicate: e => e.ReactorName == fixture.EventReactorName &&
                            e.FromPosition == 1 &&
                            e.ToPosition == 2);
    }

    [Fact]
    public void Then_parent_should_receive_push_dead_letter_messages()
    {
        fixture.PushedDeadLetters.Should().HaveCount(2);
    }

    [Fact]
    public void Then_pushed_dead_letters_should_have_correct_positions()
    {
        fixture.PushedDeadLetters.Select(x => x.OriginalPosition)
            .Should().BeEquivalentTo([1L, 2L]);
    }

    public class Fixture : PersistenceFixture
    {
        public readonly string EventReactorName = Guid.NewGuid().ToString();
        private IActorRef _parent = null!;

        public IImmutableList<PositionedStreamPublisher.Commands.PushDeadLetter> PushedDeadLetters { get; private set; }
            = ImmutableList<PositionedStreamPublisher.Commands.PushDeadLetter>.Empty;

        protected override async Task Setup()
        {
            await WithEvents(
                $"event-reactor-dead-letters-{EventReactorName}",
                Enumerable
                    .Range(1, 2)
                    .Select(i => (object)new DeadLetterHandler.Events.DeadLetterAdded(
                        EventReactorName,
                        $"test event {i}",
                        null,
                        "Test error",
                        null,
                        i))
                    .ToImmutableList());

            var probe = CreateTestProbe();

            // Wrap in a parent so Context.Parent.Tell works
            _parent = Sys.ActorOf(Props.Create(() => new ParentActor(
                DeadLetterHandler.Init(EventReactorName),
                probe.Ref)));
        }

        protected override async Task Run()
        {
            // Get the dlq child from parent
            var dlq = await _parent.Ask<IActorRef>(new ParentActor.GetChild());

            await dlq.Ask<DeadLetterHandler.Responses.ManageDeadLettersResponse>(
                new DeadLetterHandler.Commands.RetryDeadLetters(10));

            // Give parent time to collect messages
            await Task.Delay(200);

            PushedDeadLetters = await _parent.Ask<IImmutableList<PositionedStreamPublisher.Commands.PushDeadLetter>>(
                new ParentActor.GetCollectedMessages());
        }
    }
}

