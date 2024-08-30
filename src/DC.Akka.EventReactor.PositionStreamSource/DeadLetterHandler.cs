using Akka.Actor;
using Akka.Persistence;

namespace DC.Akka.EventReactor.PositionStreamSource;

public class DeadLetterHandler : ReceivePersistentActor
{
    public static class Commands
    {
        public record AddDeadLetter(object Event, Exception Error, long Position);
    }
    
    public static class Responses
    {
        public record AddDeadLetterResponse(long Position);
    }
    
    public static class Events
    {
        public record DeadLetterAdded(object Event, Exception Error);
    }
    
    private readonly string _eventReactorName;

    public DeadLetterHandler(string eventReactorName)
    {
        _eventReactorName = eventReactorName;
        
        Command<Commands.AddDeadLetter>(cmd =>
        {
            Persist(new Events.DeadLetterAdded(cmd.Event, cmd.Error), evnt =>
            {
                On(evnt);
                
                Sender.Tell(new Responses.AddDeadLetterResponse(cmd.Position));
            });
        });
    }

    public override string PersistenceId => $"dead-letters-{_eventReactorName}";

    public static Props Init(string eventReactorName)
    {
        return Props.Create(() => new DeadLetterHandler(eventReactorName));
    }

    private void On(Events.DeadLetterAdded evnt)
    {
        
    }
}