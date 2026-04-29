namespace MJ.Akka.EventReactor.Tests.TestData;

public static class EventSourcedEvents
{
    public record EventThatChangesState(string EntityId, string EventId, string NewName) : Events.IEvent;
}

