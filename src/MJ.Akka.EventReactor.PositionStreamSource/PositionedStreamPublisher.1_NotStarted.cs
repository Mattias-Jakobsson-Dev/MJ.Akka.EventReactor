using System.Collections.Immutable;
using Akka;
using Akka.Actor;
using Akka.Streams;
using Akka.Streams.Dsl;
using MJ.Akka.EventReactor.DeadLetter;

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

            _startPositionStream
                .StartFrom(_currentPosition)
                .SelectAsyncUnordered(_parallelism, async evnt =>
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

        Command<Queries.GetDeadLetters>(_ =>
        {
            var sender = Sender;

            GetDeadLetter()
                .Ask<DeadLetterHandler.Responses.GetResponse>(new DeadLetterHandler.Queries.Get())
                .ContinueWith(result =>
                {
                    sender.Tell(result.IsCompletedSuccessfully
                        ? new Responses.GetDeadLettersResponse(result.Result.DeadLetters)
                        : new Responses.GetDeadLettersResponse(ImmutableList<DeadLetterData>.Empty));
                });
        });

        Command<Commands.RetryDeadLetters>(_ =>
        {
            Sender.Tell(
                new Responses.RetryDeadLettersResponse(
                    new Exception("Can't retry dead letters when publisher is not started.")));
        });

        Command<Commands.ClearDeadLetters>(cmd =>
        {
            GetDeadLetter().Tell(new DeadLetterHandler.Commands.ClearDeadLetters(cmd.To));

            Sender.Tell(new Responses.ClearDeadLettersResponse());
        });
    }
}