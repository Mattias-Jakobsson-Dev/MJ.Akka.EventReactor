using Akka;
using Akka.Actor;
using Akka.Streams;
using Akka.Streams.Actors;
using Akka.Streams.Dsl;

namespace DC.Akka.EventReactor.PositionStreamSource;

public class PositionedStreamPublished(
    IStartPositionStream startPositionStream,
    string eventReactorName)
    : ActorPublisher<IMessageWithAck>, IWithUnboundedStash
{
    private static class InternalCommands
    {
        public record Start;

        public record StartFrom(long? Position);

        public record Completed;

        public record Failed(Exception Failure);

        public record PushEvent(EventWithPosition Event);

        public record Ack(long Position);

        public record Nack(long Position, Exception Error);
    }

    private static class InternalResponses
    {
        public record PushEventResponse;

        public record AckResponse;
    }

    private readonly Dictionary<long, object> _inFlightMessages = new();
    private readonly List<long> _inFlightPositionStore = [];

    private UniqueKillSwitch? _killSwitch;
    private bool _shouldComplete;

    public IStash Stash { get; set; } = null!;

    protected override bool Receive(object message)
    {
        var self = Self;

        switch (message)
        {
            case InternalCommands.Start:
                GetPositionKeeper()
                    .Ask<PositionedStreamPositionKeeper.Responses.GetLatestPositionResponse>(
                        new PositionedStreamPositionKeeper.Queries.GetLatestPosition())
                    .ContinueWith(result =>
                    {
                        if (result.IsCompletedSuccessfully)
                            self.Tell(new InternalCommands.StartFrom(result.Result.Position));
                        else
                            self.Tell(new InternalCommands.Failed(result.Exception ?? new Exception("Start failed")));
                    });

                return true;
            case InternalCommands.StartFrom startFrom:
                Start(startFrom.Position);

                return true;
            case InternalCommands.Completed:
                if (_inFlightPositionStore.Count != 0)
                    _shouldComplete = true;
                else
                    OnComplete();

                return true;
            case InternalCommands.Failed failed:
                OnError(failed.Failure);

                return true;
            case InternalCommands.PushEvent pushEvent:
                if (TotalDemand > 0)
                {
                    _inFlightMessages[pushEvent.Event.Position] = pushEvent.Event.Event;

                    OnNext(new PositionedEventWithAck(
                        pushEvent.Event,
                        Self));

                    Sender.Tell(new InternalResponses.PushEventResponse());
                }
                else
                {
                    Stash.Stash();
                }

                return true;
            case InternalCommands.Ack ack:
                _inFlightMessages.Remove(ack.Position);

                if (!_inFlightMessages.Any(x => x.Key < ack.Position))
                {
                    var position = _inFlightMessages.Count != 0
                        ? _inFlightMessages.Keys.Min() - 1
                        : ack.Position;

                    GetPositionKeeper()
                        .Tell(new PositionedStreamPositionKeeper.Commands.StoreLatestPosition(position));

                    _inFlightPositionStore.Add(position);
                }

                Sender.Tell(new InternalResponses.AckResponse());

                return true;
            case InternalCommands.Nack nack:
                if (!_inFlightMessages.TryGetValue(nack.Position, out var evnt))
                {
                    Sender.Tell(new InternalResponses.AckResponse());

                    return true;
                }

                var sender = Sender;

                GetDeadLetter().Ask<DeadLetterHandler.Responses.AddDeadLetterResponse>(
                        new DeadLetterHandler.Commands.AddDeadLetter(evnt, nack.Error))
                    .ContinueWith(result =>
                    {
                        if (result.IsCompletedSuccessfully)
                            self.Tell(new InternalCommands.Ack(nack.Position), sender);
                    });

                return true;
            case PositionedStreamPositionKeeper.Responses.GetLatestPositionResponse latestPositionResponse:
                if (latestPositionResponse.Position != null)
                    _inFlightPositionStore.Remove(latestPositionResponse.Position.Value);

                if (_inFlightPositionStore.Count == 0 && _shouldComplete)
                    OnComplete();

                return true;
            case Request req:
                var messagesToPush = Stash.Count > req.Count ? req.Count : Stash.Count;

                for (var i = 0; i < messagesToPush; i++)
                    Stash.Unstash();

                return true;

            case Cancel:
                _killSwitch?.Shutdown();

                return true;
        }

        return false;
    }

    protected override void PreStart()
    {
        Self.Tell(new InternalCommands.Start());

        base.PreStart();
    }

    private void Start(long? position)
    {
        var self = Self;

        _killSwitch = startPositionStream
            .StartFrom(position)
            .SelectAsyncUnordered(100, async evnt =>
            {
                await self.Ask<InternalResponses.PushEventResponse>(
                    new InternalCommands.PushEvent(evnt));

                return NotUsed.Instance;
            })
            .ViaMaterialized(KillSwitches.Single<NotUsed>(), Keep.Right)
            .ToMaterialized(Sink.ActorRef<NotUsed>(
                Self,
                new InternalCommands.Completed(),
                ex => new InternalCommands.Failed(ex)), Keep.Left)
            .Run(Context.System.Materializer());
    }

    private IActorRef GetPositionKeeper()
    {
        return Context.Child("position-keeper").GetOrElse(() =>
            Context.ActorOf(PositionedStreamPositionKeeper.Init(eventReactorName), "position-keeper"));
    }

    private IActorRef GetDeadLetter()
    {
        return Context.Child("dead-letter").GetOrElse(() =>
            Context.ActorOf(DeadLetterHandler.Init(eventReactorName), "dead-letter"));
    }

    public static Props Init(IStartPositionStream startPositionStream, string eventReactorName)
    {
        return Props.Create(() => new PositionedStreamPublished(startPositionStream, eventReactorName));
    }

    public record PositionedEventWithAck(EventWithPosition Event, IActorRef AckTo) : IMessageWithAck
    {
        public object Message => Event.Event;

        public Task Ack()
        {
            return AckTo.Ask<InternalResponses.AckResponse>(new InternalCommands.Ack(Event.Position));
        }

        public Task Nack(Exception error)
        {
            return AckTo.Ask<InternalResponses.AckResponse>(new InternalCommands.Nack(Event.Position, error));
        }
    }
}