using System.Collections.Immutable;
using Akka;
using Akka.Actor;
using Akka.Persistence;
using Akka.Streams;
using Akka.Streams.Dsl;

namespace MJ.Akka.EventReactor.PositionStreamSource;

public class PositionedStreamPublisher : ReceivePersistentActor, IWithTimers
{
    public static class Commands
    {
        public record Request(long Count);

        public record CancelDemand;

        public record Ack(long Position);

        public record Nack(long Position, Exception Error);
    }

    private static class InternalCommands
    {
        public record Start;

        public record Completed;

        public record Failed(Exception Failure);

        public record PushEvent(EventWithPosition Event);
    }

    private static class InternalResponses
    {
        public record PushEventResponse;
    }

    public static class Responses
    {
        public interface IRequestResponse;

        public record SuccessRequestResponse(IImmutableList<EventWithPosition> Events) : IRequestResponse;

        public record FailureRequestResponse(Exception Failure) : IRequestResponse;

        public record CompletedRequestResponse : IRequestResponse;

        public record AckNackResponse;
    }

    public static class Events
    {
        public record PositionUpdated(long Position);
    }

    private readonly string _eventReactorName;
    private readonly IStartPositionStream _startPositionStream;
    private readonly Dictionary<IActorRef, long> _demand = new();
    private readonly Queue<EventWithPosition> _buffer = new();
    private readonly Dictionary<long, object> _inFlightMessages = new();

    private long? _currentPosition;
    private bool _shouldComplete;

    public PositionedStreamPublisher(string eventReactorName, IStartPositionStream startPositionStream)
    {
        _eventReactorName = eventReactorName;
        _startPositionStream = startPositionStream;

        Recover<Events.PositionUpdated>(On);

        Become(NotStarted);
    }

    public override string PersistenceId => $"event-reactor-position-stream-publisher-{_eventReactorName}";

    public ITimerScheduler Timers { get; set; } = null!;

    private void NotStarted()
    {
        CommandAsync<InternalCommands.Start>(async _ =>
        {
            var cancellation = new CancellationTokenSource();

            _currentPosition ??= await _startPositionStream.GetInitialPosition();

            var self = Self;

            _startPositionStream
                .StartFrom(_currentPosition)
                .SelectAsyncUnordered(100, async evnt =>
                {
                    await self.Ask<InternalResponses.PushEventResponse>(
                        new InternalCommands.PushEvent(evnt),
                        Timeout.InfiniteTimeSpan,
                        cancellation.Token);

                    return NotUsed.Instance;
                })
                .ToMaterialized(Sink.ActorRef<NotUsed>(
                    Self,
                    new InternalCommands.Completed(),
                    ex => new InternalCommands.Failed(ex)), Keep.Left)
                .Run(Context.System.Materializer());

            Become(() => Started(cancellation));
        });
    }

    private void Started(CancellationTokenSource cancellation)
    {
        Command<InternalCommands.PushEvent>(cmd =>
        {
            _inFlightMessages[cmd.Event.Position] = cmd.Event.Event;

            var sendTo = _demand
                .OrderByDescending(x => x.Value)
                .Select(x => x.Key)
                .FirstOrDefault();

            if (sendTo != null)
            {
                PushEventsTo(ImmutableList.Create(cmd.Event), sendTo);

                _demand[sendTo]--;

                if (_demand[sendTo] <= 0)
                    _demand.Remove(sendTo);

                Sender.Tell(new InternalResponses.PushEventResponse());

                return;
            }

            if (_buffer.Count < 100)
            {
                _buffer.Enqueue(cmd.Event);

                Sender.Tell(new InternalResponses.PushEventResponse());
                
                return;
            }

            Stash.Stash();
        });
        
        Command<Commands.Request>(cmd =>
        {
            if (_buffer.Count > 0)
            {
                var currentDemand = cmd.Count;

                if (_demand.TryGetValue(Sender, out var value))
                    currentDemand += value;

                var events = new List<EventWithPosition>();

                while (currentDemand > 0 && _buffer.TryDequeue(out var evnt))
                {
                    events.Add(evnt);

                    currentDemand--;
                }

                if (events.Count != 0)
                    PushEventsTo(events.ToImmutableList(), Sender);

                if (currentDemand > 0)
                    _demand[Sender] = currentDemand;
                else
                    _demand.Remove(Sender);

                Stash.UnstashAll();
            }
            else
            {
                Stash.UnstashAll();
                
                _demand[Sender] = _demand.TryGetValue(Sender, out var value) ? value + cmd.Count : cmd.Count;
            }
        });

        Command<Commands.CancelDemand>(_ => { _demand.Remove(Sender); });

        Command<Commands.Ack>(cmd =>
        {
            Timers.Cancel($"timeout-{cmd.Position}");

            _inFlightMessages.Remove(cmd.Position);

            //TODO: Handle batching
            if (!_inFlightMessages.Any(x => x.Key < cmd.Position))
            {
                var position = _inFlightMessages.Count != 0
                    ? _inFlightMessages.Keys.Min() - 1
                    : cmd.Position;

                Persist(new Events.PositionUpdated(position), On);
            }

            if (_inFlightMessages.Count == 0 && _shouldComplete)
                CompleteGraph(cancellation);
            
            DeferAsync("done", _ => Sender.Tell(new Responses.AckNackResponse()));
        });

        Command<Commands.Nack>(cmd =>
        {
            Timers.Cancel($"timeout-{cmd.Position}");

            if (!_inFlightMessages.TryGetValue(cmd.Position, out var evnt))
            {
                Sender.Tell(new Responses.AckNackResponse());
                
                return;
            }
            
            var self = Self;
            var sender = Sender;
            
            GetDeadLetter().Ask<DeadLetterHandler.Responses.AddDeadLetterResponse>(
                    new DeadLetterHandler.Commands.AddDeadLetter(evnt, cmd.Error))
                .ContinueWith(result =>
                {
                    if (result.IsCompletedSuccessfully)
                        self.Tell(new Commands.Ack(cmd.Position), sender);
                    else
                        self.Tell(cmd, sender);
                });
        });

        Command<InternalCommands.Completed>(_ =>
        {
            if (_inFlightMessages.Count != 0)
                _shouldComplete = true;
            else
                CompleteGraph(cancellation);
        });

        Command<InternalCommands.Failed>(cmd =>
        {
            cancellation.Cancel();

            Become(() => Failed(cmd.Failure));
        });
    }

    private void Completed()
    {
        Command<Commands.Request>(_ => { Sender.Tell(new Responses.CompletedRequestResponse()); });
    }

    private void Failed(Exception failure)
    {
        Command<Commands.Request>(_ => { Sender.Tell(new Responses.FailureRequestResponse(failure)); });
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

    protected override void PreStart()
    {
        Self.Tell(new InternalCommands.Start());

        base.PreStart();
    }

    private void On(Events.PositionUpdated evnt)
    {
        _currentPosition = evnt.Position;
    }
}