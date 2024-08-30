namespace DC.Akka.EventReactor.Tests.TestData;

public static class Events
{
    public record HandledEvent(string EventId) : IEvent;

    public record UnHandledEvent(string EventId) : IEvent;

    public record EventThatFails(string EventId, Exception Exception) : IEvent;
    
    public interface IEvent
    {
        string EventId { get; }
    }
}