using System.Collections.Immutable;
using Akka.Actor;
using FluentAssertions;
using MJ.Akka.EventReactor.PositionStreamSource;
using Xunit;

namespace MJ.Akka.EventReactor.Tests.PositionStreamSource.PositionedStreamPublisherTests;

/// <summary>
/// When the max retry count is exceeded and UseDeadLetter is true,
/// the event should be sent to the dead letter handler instead of failing.
/// </summary>
public class When_nacking_beyond_retry_limit_with_dead_letter(
    When_nacking_beyond_retry_limit_with_dead_letter.Fixture fixture)
    : IClassFixture<When_nacking_beyond_retry_limit_with_dead_letter.Fixture>
{
    [Fact]
    public void Then_ack_nack_response_was_returned_for_last_nack()
    {
        fixture.LastNackResponse
            .Should().BeOfType<PositionedStreamPublisher.Responses.AckNackResponse>();
    }

    public class Fixture : PublisherTestFixture
    {
        protected override PositionedStreamSettings Settings => new(
            Parallelism: 1,
            PositionBatchSize: 100,
            PositionWriteInterval: TimeSpan.FromSeconds(5),
            UseDeadLetter: true,
            MaxRetries: 1);

        protected override IReadOnlyList<EventWithPosition> Events =>
            [MakeEvent(10)];

        public object? LastNackResponse { get; private set; }

        protected override async Task Run()
        {
            Publisher.Tell(new PositionedStreamPublisher.Commands.Start());
            await Task.Delay(200);

            // Pull initial event
            Publisher.Tell(new PositionedStreamPublisher.Commands.Request(1), WorkerProbe.Ref);
            var delivery = await WorkerProbe
                .ExpectMsgAsync<PositionedStreamPublisher.Responses.SuccessRequestResponse>(
                    TimeSpan.FromSeconds(3));

            var position = delivery.EventsToHandle[0].Position;

            // Exhaust retries (MaxRetries = 1, so nack twice: retry + dead letter)
            for (var i = 0; i <= Settings.MaxRetries; i++)
            {
                if (i < Settings.MaxRetries)
                    Publisher.Tell(new PositionedStreamPublisher.Commands.Request(1), WorkerProbe.Ref);

                LastNackResponse = await Publisher.Ask<object>(
                    new PositionedStreamPublisher.Commands.Nack(position, new Exception($"error {i}")),
                    TimeSpan.FromSeconds(3));

                if (i < Settings.MaxRetries)
                {
                    // consume the redelivery
                    await WorkerProbe
                        .ExpectMsgAsync<PositionedStreamPublisher.Responses.SuccessRequestResponse>(
                            TimeSpan.FromSeconds(3));
                }
            }
        }
    }
}




