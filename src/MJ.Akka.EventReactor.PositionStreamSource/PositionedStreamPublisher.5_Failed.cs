using System.Collections.Immutable;
using Akka.Actor;
using MJ.Akka.EventReactor.DeadLetter;

namespace MJ.Akka.EventReactor.PositionStreamSource;

public partial class PositionedStreamPublisher
{
    private void Failed(Exception failure)
    {
        Command<Commands.Request>(_ =>
        {
            Sender.Tell(new Responses.FailureRequestResponse(failure));
        });
        
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
                    new Exception("Can't retry dead letters when publisher is failed.")));
        });

        Command<Commands.ClearDeadLetters>(cmd =>
        {
            GetDeadLetter().Tell(new DeadLetterHandler.Commands.ClearDeadLetters(cmd.To));

            Sender.Tell(new Responses.ClearDeadLettersResponse());
        });
    }
}