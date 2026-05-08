using System.Collections.Immutable;
using Akka.Actor;
using FluentAssertions;
using MJ.Akka.EventReactor.PositionStreamSource;
using Xunit;

namespace MJ.Akka.EventReactor.Tests.PositionStreamSource.PositionedStreamPublisherTests;

/// <summary>
/// When an event is acked after being retried, the ack should succeed 
/// and the publisher should remain alive and healthy.
/// </summary>
public class When_acking_after_retry(When_acking_after_retry.Fixture fixture)
    : IClassFixture<When_acking_after_retry.Fixture>
{
    [Fact]
    public void Then_ack_response_is_received()
    {
        fixture.AckResponse.Should().BeOfType<PositionedStreamPublisher.Responses.AckNackResponse>();
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
            [MakeEvent(20)];

        public object? AckResponse { get; private set; }

        protected override async Task Run()
        {
            Publisher.Tell(new PositionedStreamPublisher.Commands.Start());
            await Task.Delay(200);

            // Initial delivery
            Publisher.Tell(new PositionedStreamPublisher.Commands.Request(1), WorkerProbe.Ref);
            var delivery = await WorkerProbe
                .ExpectMsgAsync<PositionedStreamPublisher.Responses.SuccessRequestResponse>(
                    TimeSpan.FromSeconds(3));

            var position = delivery.EventsToHandle[0].Position;

            // Nack and collect redelivery
            Publisher.Tell(new PositionedStreamPublisher.Commands.Request(1), WorkerProbe.Ref);
            await Publisher.Ask<object>(
                new PositionedStreamPublisher.Commands.Nack(position, new Exception("transient")),
                TimeSpan.FromSeconds(3));

            var redelivery = await WorkerProbe
                .ExpectMsgAsync<PositionedStreamPublisher.Responses.SuccessRequestResponse>(
                    TimeSpan.FromSeconds(3));

            // Ack the redelivered event - retry count should be cleared
            AckResponse = await Publisher.Ask<object>(
                new PositionedStreamPublisher.Commands.Ack(redelivery.EventsToHandle[0].Position),
                TimeSpan.FromSeconds(3));
        }
    }
}





