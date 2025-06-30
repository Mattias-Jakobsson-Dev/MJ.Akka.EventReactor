using Akka.Actor;
using Akka.Persistence;

namespace MJ.Akka.EventReactor.PositionStreamSource;

public class DeadLetterHandler : ReceivePersistentActor
{
    public static class Commands
    {
        public record AddDeadLetter(object Event, Exception Error);
    }
    
    public static class Responses
    {
        public record AddDeadLetterResponse;
    }
    
    public static class Events
    {
        public record DeadLetterAdded(object Event, Exception Error);
    }
    
    private readonly string _eventReactorName;

    public DeadLetterHandler(string eventReactorName)
    {
        _eventReactorName = eventReactorName;
        
        Recover<Events.DeadLetterAdded>(On);
        
        Command<Commands.AddDeadLetter>(cmd =>
        {
            //TODO: Handle retries
            Persist(new Events.DeadLetterAdded(cmd.Event, cmd.Error), evnt =>
            {
                On(evnt);
                
                Sender.Tell(new Responses.AddDeadLetterResponse());
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