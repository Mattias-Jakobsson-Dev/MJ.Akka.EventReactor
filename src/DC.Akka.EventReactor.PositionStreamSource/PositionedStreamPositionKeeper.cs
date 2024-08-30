using Akka.Actor;
using Akka.Persistence;

namespace DC.Akka.EventReactor.PositionStreamSource;

public class PositionedStreamPositionKeeper : ReceivePersistentActor
{
    public static class Commands
    {
        public record StoreLatestPosition(long Position);
    }
    
    public static class Queries
    {
        public record GetLatestPosition;
    }
    
    public static class Responses
    {
        public record GetLatestPositionResponse(long? Position);
    }
    
    public static class Events
    {
        public record PositionUpdated(long Position);
    }

    private readonly string _eventReactorName;
    private long? _currentPosition;
    
    public override string PersistenceId => $"position-keepers-{_eventReactorName}";

    public PositionedStreamPositionKeeper(string eventReactorName)
    {
        _eventReactorName = eventReactorName;
        
        Recover<Events.PositionUpdated>(On);
        
        Command<Commands.StoreLatestPosition>(cmd =>
        {
            if (_currentPosition == null || cmd.Position > _currentPosition)
            {
                Persist(
                    new Events.PositionUpdated(cmd.Position),
                    evnt =>
                    {
                        On(evnt);
                        
                        if (LastSequenceNr % 10 == 0 && LastSequenceNr > 10)
                            DeleteMessages(LastSequenceNr - 10);
                        
                        Sender.Tell(new Responses.GetLatestPositionResponse(_currentPosition));
                    });
            }
        });
        
        Command<Queries.GetLatestPosition>(_ =>
        {
            Sender.Tell(new Responses.GetLatestPositionResponse(_currentPosition));
        });
    }

    private void On(Events.PositionUpdated evnt)
    {
        _currentPosition = evnt.Position;
    }

    public static Props Init(string eventReactorName)
    {
        return Props.Create(() => new PositionedStreamPositionKeeper(eventReactorName));
    }
}