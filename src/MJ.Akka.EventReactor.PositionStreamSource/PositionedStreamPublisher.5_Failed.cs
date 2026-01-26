using Akka.Actor;

namespace MJ.Akka.EventReactor.PositionStreamSource;

public partial class PositionedStreamPublisher
{
    private void Failed(Exception failure)
    {
        Command<Commands.Request>(_ =>
        {
            Sender.Tell(new Responses.FailureRequestResponse(failure));
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