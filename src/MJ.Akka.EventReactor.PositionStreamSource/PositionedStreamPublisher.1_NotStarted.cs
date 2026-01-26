using Akka;
using Akka.Actor;
using Akka.Event;
using Akka.Streams;
using Akka.Streams.Dsl;

namespace MJ.Akka.EventReactor.PositionStreamSource;

public partial class PositionedStreamPublisher
{
    private void NotStarted()
    {
        CommandAsync<Commands.Start>(async _ =>
        {
            var cancellation = new CancellationTokenSource();

            _currentPosition ??= await _startPositionStream.GetInitialPosition();

            var self = Self;
            
            Log.Info("Starting positioned stream event reactor {0} from position {1}", _eventReactorName, _currentPosition);
            
            GetDeadLetter().Tell(new DeadLetterHandler.Commands.RetryPending());

            _startPositionStream
                .StartFrom(_currentPosition)
                .SelectAsyncUnordered(_settings.Parallelism, async evnt =>
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

            Stash.UnstashAll();

            Become(() => Started(cancellation));
        });

        Command<Commands.Request>(_ => { Stash.Stash(); });
    }
}