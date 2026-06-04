using System.Collections.Immutable;
using Akka.Actor;
using FluentAssertions;
using MJ.Akka.EventReactor.PositionStreamSource;
using Xunit;

namespace MJ.Akka.EventReactor.Tests.PositionStreamSource.PositionedStreamPublisherTests;

/// <summary>
/// A single ack should result in exactly one PositionUpdated event being persisted
/// (after the write interval elapses), containing the acked position.
/// </summary>
public class When_acking_single_event_writes_position(When_acking_single_event_writes_position.Fixture fixture)
    : IClassFixture<When_acking_single_event_writes_position.Fixture>
{
    [Fact]
    public void Then_one_position_updated_event_is_persisted()
    {
        fixture.StoredEvents
            .Where(x => x.Event is PositionedStreamPublisher.Events.PositionUpdated)
            .Should().HaveCount(1);
    }

    [Fact]
    public void Then_the_correct_position_is_written()
    {
        fixture.StoredEvents
            .Select(x => x.Event)
            .OfType<PositionedStreamPublisher.Events.PositionUpdated>()
            .Single()
            .Position
            .Should().Be(42);
    }

    public class Fixture : PublisherTestFixture
    {
        protected override PositionedStreamSettings Settings => new(
            Parallelism: 1,
            PositionBatchSize: 100,
            PositionWriteInterval: TimeSpan.FromMilliseconds(200),
            UseDeadLetter: false,
            MaxRetries: 0);

        protected override IReadOnlyList<EventWithPosition> Events => [MakeEvent(42)];

        protected override async Task Run()
        {
            Publisher.Tell(new PositionedStreamPublisher.Commands.Start());

            Publisher.Tell(new PositionedStreamPublisher.Commands.Request(1), WorkerProbe.Ref);
            var delivery = await WorkerProbe
                .ExpectMsgAsync<PositionedStreamPublisher.Responses.SuccessRequestResponse>(
                    TimeSpan.FromSeconds(5));

            await Publisher.Ask<PositionedStreamPublisher.Responses.AckNackResponse>(
                new PositionedStreamPublisher.Commands.Ack(delivery.EventsToHandle[0].Position),
                TimeSpan.FromSeconds(3));

            // Wait for the write interval to elapse and the position to be persisted
            await Task.Delay(500);
        }
    }
}

/// <summary>
/// When multiple events are acked before the write interval elapses, they should be
/// batched together into a single PositionUpdated write (not one write per ack).
/// </summary>
public class When_acking_multiple_events_batches_position_writes(When_acking_multiple_events_batches_position_writes.Fixture fixture)
    : IClassFixture<When_acking_multiple_events_batches_position_writes.Fixture>
{
    [Fact]
    public void Then_only_one_position_updated_event_is_persisted()
    {
        fixture.StoredEvents
            .Where(x => x.Event is PositionedStreamPublisher.Events.PositionUpdated)
            .Should().HaveCount(1, "multiple acks within the same interval should be batched into a single write");
    }

    [Fact]
    public void Then_the_highest_position_is_written()
    {
        fixture.StoredEvents
            .Select(x => x.Event)
            .OfType<PositionedStreamPublisher.Events.PositionUpdated>()
            .Single()
            .Position
            .Should().Be(30, "the max position of the batch should be persisted");
    }

    public class Fixture : PublisherTestFixture
    {
        // Long interval so all 3 acks land in the same batch window
        protected override PositionedStreamSettings Settings => new(
            Parallelism: 1,
            PositionBatchSize: 100,
            PositionWriteInterval: TimeSpan.FromMilliseconds(500),
            UseDeadLetter: false,
            MaxRetries: 0);

        protected override IReadOnlyList<EventWithPosition> Events =>
            [MakeEvent(10), MakeEvent(20), MakeEvent(30)];

        protected override async Task Run()
        {
            Publisher.Tell(new PositionedStreamPublisher.Commands.Start());

            // Collect all 3 event deliveries (may arrive one at a time with parallelism=1)
            var positions = await CollectAllPositions(3, TimeSpan.FromSeconds(5));

            // Ack all events quickly, within the same write interval
            foreach (var pos in positions)
            {
                await Publisher.Ask<PositionedStreamPublisher.Responses.AckNackResponse>(
                    new PositionedStreamPublisher.Commands.Ack(pos),
                    TimeSpan.FromSeconds(3));
            }

            // Wait for the write interval to elapse and the position to be persisted
            await Task.Delay(800);
        }

        private async Task<List<long>> CollectAllPositions(int count, TimeSpan timeout)
        {
            var positions = new List<long>();
            while (positions.Count < count)
            {
                Publisher.Tell(new PositionedStreamPublisher.Commands.Request(count - positions.Count), WorkerProbe.Ref);
                var delivery = await WorkerProbe
                    .ExpectMsgAsync<PositionedStreamPublisher.Responses.SuccessRequestResponse>(timeout);
                positions.AddRange(delivery.EventsToHandle.Select(x => x.Position));
            }
            return positions;
        }
    }
}

/// <summary>
/// When the batch size is reached, positions should be written without waiting for
/// the full write interval to elapse.
/// </summary>
public class When_batch_size_is_reached_position_is_written_immediately(When_batch_size_is_reached_position_is_written_immediately.Fixture fixture)
    : IClassFixture<When_batch_size_is_reached_position_is_written_immediately.Fixture>
{
    [Fact]
    public void Then_position_is_persisted_without_waiting_for_full_interval()
    {
        fixture.ElapsedBeforeWrite.Should().BeLessThan(
            TimeSpan.FromSeconds(10),
            "a full batch should trigger a write before the 30-second write interval");
    }

    [Fact]
    public void Then_position_updated_event_is_persisted()
    {
        fixture.StoredEvents
            .Where(x => x.Event is PositionedStreamPublisher.Events.PositionUpdated)
            .Should().NotBeEmpty();
    }

    [Fact]
    public void Then_the_highest_position_is_written()
    {
        fixture.StoredEvents
            .Select(x => x.Event)
            .OfType<PositionedStreamPublisher.Events.PositionUpdated>()
            .Max(x => x.Position)
            .Should().Be(30);
    }

    public class Fixture : PublisherTestFixture
    {
        // Very long write interval - position should be written before this elapses
        // because the batch size of 3 is reached first
        protected override PositionedStreamSettings Settings => new(
            Parallelism: 1,
            PositionBatchSize: 3,
            PositionWriteInterval: TimeSpan.FromSeconds(30),
            UseDeadLetter: false,
            MaxRetries: 0);

        protected override IReadOnlyList<EventWithPosition> Events =>
            [MakeEvent(10), MakeEvent(20), MakeEvent(30)];

        public TimeSpan ElapsedBeforeWrite { get; private set; }

        protected override async Task Run()
        {
            Publisher.Tell(new PositionedStreamPublisher.Commands.Start());

            // Collect all 3 events before starting the clock
            var positions = await CollectAllPositions(3, TimeSpan.FromSeconds(5));

            var start = DateTime.UtcNow;

            // Ack exactly the batch size (3) — this should trigger a write without
            // waiting for the full 30-second interval
            foreach (var pos in positions)
            {
                await Publisher.Ask<PositionedStreamPublisher.Responses.AckNackResponse>(
                    new PositionedStreamPublisher.Commands.Ack(pos),
                    TimeSpan.FromSeconds(3));
            }

            // Wait just enough time for the batch to be flushed and persisted (far less than 30s)
            await Task.Delay(1000);

            ElapsedBeforeWrite = DateTime.UtcNow - start;
        }

        private async Task<List<long>> CollectAllPositions(int count, TimeSpan timeout)
        {
            var positions = new List<long>();
            while (positions.Count < count)
            {
                Publisher.Tell(new PositionedStreamPublisher.Commands.Request(count - positions.Count), WorkerProbe.Ref);
                var delivery = await WorkerProbe
                    .ExpectMsgAsync<PositionedStreamPublisher.Responses.SuccessRequestResponse>(timeout);
                positions.AddRange(delivery.EventsToHandle.Select(x => x.Position));
            }
            return positions;
        }
    }
}

/// <summary>
/// When a higher-positioned event is acked but a lower-positioned event is still
/// in-flight, the position should NOT be advanced (no PositionUpdated event persisted).
/// </summary>
public class When_acking_higher_position_with_lower_still_in_flight(When_acking_higher_position_with_lower_still_in_flight.Fixture fixture)
    : IClassFixture<When_acking_higher_position_with_lower_still_in_flight.Fixture>
{
    [Fact]
    public void Then_no_position_updated_event_is_persisted()
    {
        fixture.StoredEvents
            .Where(x => x.Event is PositionedStreamPublisher.Events.PositionUpdated)
            .Should().BeEmpty("position must not advance past an in-flight message at a lower position");
    }

    public class Fixture : PublisherTestFixture
    {
        protected override PositionedStreamSettings Settings => new(
            Parallelism: 1,
            PositionBatchSize: 100,
            PositionWriteInterval: TimeSpan.FromMilliseconds(200),
            UseDeadLetter: false,
            MaxRetries: 0);

        protected override IReadOnlyList<EventWithPosition> Events =>
            [MakeEvent(10), MakeEvent(20)];

        protected override async Task Run()
        {
            Publisher.Tell(new PositionedStreamPublisher.Commands.Start());

            // Collect both events
            var positions = await CollectAllPositions(2, TimeSpan.FromSeconds(5));
            var ordered = positions.OrderBy(x => x).ToList();

            // Only ack the higher position — leave the lower one in-flight
            await Publisher.Ask<PositionedStreamPublisher.Responses.AckNackResponse>(
                new PositionedStreamPublisher.Commands.Ack(ordered[1]),
                TimeSpan.FromSeconds(3));

            // Wait for the write interval to elapse and confirm no position is written
            await Task.Delay(500);
        }

        private async Task<List<long>> CollectAllPositions(int count, TimeSpan timeout)
        {
            var positions = new List<long>();
            while (positions.Count < count)
            {
                Publisher.Tell(new PositionedStreamPublisher.Commands.Request(count - positions.Count), WorkerProbe.Ref);
                var delivery = await WorkerProbe
                    .ExpectMsgAsync<PositionedStreamPublisher.Responses.SuccessRequestResponse>(timeout);
                positions.AddRange(delivery.EventsToHandle.Select(x => x.Position));
            }
            return positions;
        }
    }
}

/// <summary>
/// When the lower-positioned in-flight message is acked after the higher one,
/// the position should advance to the higher position.
/// </summary>
public class When_all_in_flight_messages_are_acked_position_advances(When_all_in_flight_messages_are_acked_position_advances.Fixture fixture)
    : IClassFixture<When_all_in_flight_messages_are_acked_position_advances.Fixture>
{
    [Fact]
    public void Then_position_updated_event_is_eventually_persisted()
    {
        fixture.StoredEvents
            .Where(x => x.Event is PositionedStreamPublisher.Events.PositionUpdated)
            .Should().NotBeEmpty("once all in-flight messages are acked the position should be written");
    }

    [Fact]
    public void Then_position_is_advanced_to_the_highest_acked_position()
    {
        fixture.StoredEvents
            .Select(x => x.Event)
            .OfType<PositionedStreamPublisher.Events.PositionUpdated>()
            .Max(x => x.Position)
            .Should().Be(20);
    }

    public class Fixture : PublisherTestFixture
    {
        protected override PositionedStreamSettings Settings => new(
            Parallelism: 1,
            PositionBatchSize: 100,
            PositionWriteInterval: TimeSpan.FromMilliseconds(200),
            UseDeadLetter: false,
            MaxRetries: 0);

        protected override IReadOnlyList<EventWithPosition> Events =>
            [MakeEvent(10), MakeEvent(20)];

        protected override async Task Run()
        {
            Publisher.Tell(new PositionedStreamPublisher.Commands.Start());

            // Collect both events
            var positions = await CollectAllPositions(2, TimeSpan.FromSeconds(5));
            var ordered = positions.OrderBy(x => x).ToList();

            // Ack higher position first, lower still in-flight — position must NOT advance yet
            await Publisher.Ask<PositionedStreamPublisher.Responses.AckNackResponse>(
                new PositionedStreamPublisher.Commands.Ack(ordered[1]),
                TimeSpan.FromSeconds(3));

            // Now ack the lower position — both are done, position should advance to 20
            await Publisher.Ask<PositionedStreamPublisher.Responses.AckNackResponse>(
                new PositionedStreamPublisher.Commands.Ack(ordered[0]),
                TimeSpan.FromSeconds(3));

            // Wait for the write interval to elapse
            await Task.Delay(500);
        }

        private async Task<List<long>> CollectAllPositions(int count, TimeSpan timeout)
        {
            var positions = new List<long>();
            while (positions.Count < count)
            {
                Publisher.Tell(new PositionedStreamPublisher.Commands.Request(count - positions.Count), WorkerProbe.Ref);
                var delivery = await WorkerProbe
                    .ExpectMsgAsync<PositionedStreamPublisher.Responses.SuccessRequestResponse>(timeout);
                positions.AddRange(delivery.EventsToHandle.Select(x => x.Position));
            }
            return positions;
        }
    }
}

