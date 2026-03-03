using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Akka.Actor;
using FluentAssertions;
using MJ.Akka.EventReactor.PositionStreamSource;
using Xunit;

namespace MJ.Akka.EventReactor.Tests.PositionStreamSource.DeadLetterHandlerTests;

public class When_adding_a_dead_letter(When_adding_a_dead_letter.Fixture fixture)
    : IClassFixture<When_adding_a_dead_letter.Fixture>
{
    [Fact]
    public void Then_one_event_should_be_stored()
    {
        fixture.StoredEvents.Should().HaveCount(1);
    }

    [Fact]
    public void Then_stored_event_should_be_dead_letter_added_event()
    {
        fixture.StoredEvents.Should().HaveEvents<DeadLetterHandler.Events.DeadLetterAdded>(
            predicate: e => e.ReactorName == fixture.EventReactorName &&
                            e.OriginalPosition == 42 &&
                            e.ErrorMessage == "Something went wrong");
    }

    public class Fixture : PersistenceFixture
    {
        public readonly string EventReactorName = Guid.NewGuid().ToString();
        private IActorRef _dlq = null!;

        protected override Task Setup()
        {
            _dlq = Sys.ActorOf(DeadLetterHandler.Init(EventReactorName));
            return Task.CompletedTask;
        }

        protected override async Task Run()
        {
            await _dlq.Ask<DeadLetterHandler.Responses.ManageDeadLettersResponse>(
                new DeadLetterHandler.Commands.AddDeadLetter(
                    42,
                    "test event",
                    new Dictionary<string, object?> { ["key"] = "value" },
                    new Exception("Something went wrong")));
        }
    }
}

