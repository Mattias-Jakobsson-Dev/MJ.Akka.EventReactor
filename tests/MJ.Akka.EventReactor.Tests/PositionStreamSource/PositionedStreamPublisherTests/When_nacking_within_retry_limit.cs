using System.Collections.Immutable;
using Akka.Actor;
using FluentAssertions;
using MJ.Akka.EventReactor.PositionStreamSource;
using Xunit;

namespace MJ.Akka.EventReactor.Tests.PositionStreamSource.PositionedStreamPublisherTests;

/// <summary>
/// When a nack is received and the retry limit has not been reached,
/// the event should be re-delivered to a subscriber.
/// </summary>
public class When_nacking_within_retry_limit(When_nacking_within_retry_limit.Fixture fixture)
    : IClassFixture<When_nacking_within_retry_limit.Fixture>
{
    [Fact]
    public void Then_ack_nack_response_is_received()
    {
        fixture.NackResponse.Should().BeOfType<PositionedStreamPublisher.Responses.AckNackResponse>();
    }

    [Fact]
    public void Then_event_is_redelivered_to_subscriber()
    {
        fixture.RedeliveredResponse.Should().NotBeNull();
        fixture.RedeliveredResponse!.EventsToHandle.Should().ContainSingle(e => e.Position == 10);
    }

    public class Fixture : PublisherTestFixture
    {
        protected override PositionedStreamSettings Settings => new(
            Parallelism: 1,
            PositionBatchSize: 100,
            PositionWriteInterval: TimeSpan.FromSeconds(5),
            UseDeadLetter: false,
            MaxRetries: 3);

        protected override IReadOnlyList<EventWithPosition> Events =>
            [MakeEvent(10)];

        public object? NackResponse { get; private set; }
        public PositionedStreamPublisher.Responses.SuccessRequestResponse? RedeliveredResponse { get; private set; }

        protected override async Task Run()
        {
            Publisher.Tell(new PositionedStreamPublisher.Commands.Start());
            await Task.Delay(200);

            // Register demand - event will be delivered to WorkerProbe
            Publisher.Tell(new PositionedStreamPublisher.Commands.Request(1), WorkerProbe.Ref);

            // Receive the initial delivery
            var initial = await WorkerProbe
                .ExpectMsgAsync<PositionedStreamPublisher.Responses.SuccessRequestResponse>(
                    TimeSpan.FromSeconds(3));

            // Register demand again so redelivery has somewhere to go
            Publisher.Tell(new PositionedStreamPublisher.Commands.Request(1), WorkerProbe.Ref);

            // Nack the event (first retry)
            NackResponse = await Publisher.Ask<object>(
                new PositionedStreamPublisher.Commands.Nack(
                    initial.EventsToHandle[0].Position,
                    new Exception("transient error")),
                TimeSpan.FromSeconds(3));

            // Expect re-delivery
            RedeliveredResponse = await WorkerProbe
                .ExpectMsgAsync<PositionedStreamPublisher.Responses.SuccessRequestResponse>(
                    TimeSpan.FromSeconds(3));
        }
    }
}


