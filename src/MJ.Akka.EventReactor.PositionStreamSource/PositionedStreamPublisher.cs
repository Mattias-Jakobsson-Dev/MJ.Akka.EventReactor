using System.Collections.Immutable;
using Akka;
using Akka.Actor;
using Akka.Persistence;
using Akka.Streams;
using Akka.Streams.Dsl;
using MJ.Akka.EventReactor.DeadLetter;

namespace MJ.Akka.EventReactor.PositionStreamSource;

public partial class PositionedStreamPublisher : ReceivePersistentActor, IWithTimers
{
    public static class Commands
    {
        public record Start;
        
        public record Request(long Count);

        public record CancelDemand;

        public record Ack(long Position);

        public record Nack(long Position, Exception Error);

        public record RetryDeadLetters(long To);
        
        public record ClearDeadLetters(long To);

        public record PushDeadLetter(long OriginalPosition, object Message, Dictionary<string, object?> MetaData);

        public record LastDeadLetterPushed;
    }
    
    private static class InternalCommands
    {
        public record Completed;

        public record Failed(Exception Failure);

        public record PushEvent(EventWithPosition Event);

        public record WritePosition(IImmutableList<long> Positions);
    }

    public static class Queries
    {
        public record GetDeadLetters;
    }
    
    private static class InternalResponses
    {
        public record PushEventResponse;
    }

    public static class Responses
    {
        public interface IRequestResponse;

        public record SuccessRequestResponse(IImmutableList<EventWithPosition> EventsToHandle) : IRequestResponse;

        public record FailureRequestResponse(Exception Failure) : IRequestResponse;

        public record CompletedRequestResponse : IRequestResponse;

        public record AckNackResponse;

        public record GetDeadLettersResponse(IImmutableList<DeadLetterData> DeadLetters);
        
        public record RetryDeadLettersResponse(Exception? Error = null);
        
        public record ClearDeadLettersResponse(Exception? Error = null);
    }

    public static class Events
    {
        public record PositionUpdated(long Position);
    }

    private readonly string _eventReactorName;
    private readonly IStartPositionStream _startPositionStream;
    private readonly PositionedStreamSettings _settings;
    private readonly Dictionary<IActorRef, long> _demand = new();
    private readonly Queue<EventWithPosition> _buffer = new();
    private readonly Dictionary<long, (object message, Dictionary<string, object?> metadata)> _inFlightMessages = new();
    private readonly HashSet<long> _positionsFromDeadLetters = [];
    private ISourceQueueWithComplete<InternalCommands.WritePosition>? _positionUpdatedQueue;

    private long? _currentPosition;
    private bool _shouldComplete;

    public PositionedStreamPublisher(
        string eventReactorName,
        IStartPositionStream startPositionStream,
        PositionedStreamSettings settings)
    {
        _eventReactorName = eventReactorName;
        _startPositionStream = startPositionStream;
        _settings = settings;

        Recover<Events.PositionUpdated>(On);

        Become(NotStarted);
    }

    public override string PersistenceId => $"event-reactor-position-stream-publisher-{_eventReactorName}";
    
    public ITimerScheduler Timers { get; set; } = null!;

    protected override void PreStart()
    {
        var self = Self;
        
        _positionUpdatedQueue = Source
            .Queue<InternalCommands.WritePosition>(1_000, OverflowStrategy.DropHead)
            .GroupedWithin(_settings.PositionBatchSize, _settings.PositionWriteInterval)
            .Select(x =>
                new InternalCommands.WritePosition(x.SelectMany(y => y.Positions).Distinct().ToImmutableList()))
            .Select(x =>
            {
                self.Tell(x);

                return NotUsed.Instance;
            })
            .ToMaterialized(Sink.Ignore<NotUsed>(), Keep.Left)
            .Run(Context.System.Materializer());
    }

    private void PushEventsTo(IImmutableList<EventWithPosition> events, IActorRef receiver)
    {
        receiver.Tell(new Responses.SuccessRequestResponse(events));

        foreach (var evnt in events)
        {
            Timers.StartSingleTimer(
                $"timeout-{evnt.Position}",
                new Commands.Nack(evnt.Position, new Exception("Message timed out")),
                TimeSpan.FromSeconds(10));
        }
    }

    private void CompleteGraph(CancellationTokenSource cancellation)
    {
        foreach (var item in _demand)
            item.Key.Tell(new Responses.CompletedRequestResponse());

        _demand.Clear();

        cancellation.Cancel();

        Become(Completed);
    }

    private IActorRef GetDeadLetter()
    {
        return Context.Child("dead-letter").GetOrElse(() =>
            Context.ActorOf(DeadLetterHandler.Init(_eventReactorName), "dead-letter"));
    }
    
    private void On(Events.PositionUpdated evnt)
    {
        _currentPosition = evnt.Position;
    }
}