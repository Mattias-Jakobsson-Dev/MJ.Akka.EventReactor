using System.Collections.Immutable;
using Akka.Actor;
using FluentAssertions;
using MJ.Akka.EventReactor.PositionStreamSource;
using Xunit;

namespace MJ.Akka.EventReactor.Tests.PositionStreamSource.PositionedStreamPublisherTests;

/// <summary>
/// When the max retry count is exceeded and UseDeadLetter is false,
/// the publisher should transition to the Failed state.
/// </summary>
public class When_nacking_beyond_retry_limit_without_dead_letter(
    When_nacking_beyond_retry_limit_without_dead_letter.Fixture fixture)
    : IClassFixture<When_nacking_beyond_retry_limit_without_dead_letter.Fixture>
{
    [Fact]
    public void Then_subsequent_requests_receive_failure_response()
    {
        fixture.ResponseAfterExhaustedRetries
            .Should().BeOfType<PositionedStreamPublisher.Responses.FailureRequestResponse>();
    }

    public class Fixture : PublisherTestFixture
    {
        protected override PositionedStreamSettings Settings => new(
            Parallelism: 1,
            PositionBatchSize: 100,
            PositionWriteInterval: TimeSpan.FromSeconds(5),
            UseDeadLetter: false,
            MaxRetries: 2);

        protected override IReadOnlyList<EventWithPosition> Events =>
            [MakeEvent(10)];

        public object? ResponseAfterExhaustedRetries { get; private set; }

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

            // Exhaust all retries (MaxRetries = 2, so nack 3 times total)
            for (var i = 0; i <= Settings.MaxRetries; i++)
            {
                if (i < Settings.MaxRetries)
                    Publisher.Tell(new PositionedStreamPublisher.Commands.Request(1), WorkerProbe.Ref);

                await Publisher.Ask<object>(
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

            // Publisher is now in Failed state - a new Request should return FailureRequestResponse
            ResponseAfterExhaustedRetries = await Publisher.Ask<object>(
                new PositionedStreamPublisher.Commands.Request(1),
                TimeSpan.FromSeconds(3));
        }
    }
}


