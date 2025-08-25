using System.Collections.Immutable;
using Akka.Actor;
using MJ.Akka.EventReactor.DeadLetter;

namespace MJ.Akka.EventReactor.PositionStreamSource;

public partial class PositionedStreamPublisher
{
    private void Completed()
    {
        Command<Commands.Request>(_ => { Sender.Tell(new Responses.CompletedRequestResponse()); });

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

        Command<Commands.RetryDeadLetters>(cmd =>
        {
            GetDeadLetter().Tell(new DeadLetterHandler.Commands.RetryDeadLetters(cmd.To));
            
            Become(RetryingDeadLetters);

            Sender.Tell(new Responses.RetryDeadLettersResponse());
        });

        Command<Commands.ClearDeadLetters>(cmd =>
        {
            GetDeadLetter().Tell(new DeadLetterHandler.Commands.ClearDeadLetters(cmd.To));

            Sender.Tell(new Responses.ClearDeadLettersResponse());
        });
        
        Command<InternalCommands.WritePosition>(cmd =>
        {
            var position = cmd.Positions
                .Where(x => !_inFlightMessages.Any(y => y.Key < x))
                .Max();

            if (position <= 0) 
                return;
            
            Persist(new Events.PositionUpdated(position), On);

            if (LastSequenceNr % 10 == 0 && LastSequenceNr > 0)
                DeleteMessages(LastSequenceNr - 5);
        });
    }
}