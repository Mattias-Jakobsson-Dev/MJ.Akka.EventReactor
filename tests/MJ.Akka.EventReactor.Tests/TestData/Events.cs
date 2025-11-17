using System.Collections.Immutable;

namespace MJ.Akka.EventReactor.Tests.TestData;

public static class Events
{
    public record HandledEvent(string EntityId, string EventId) : IEvent;

    public record UnHandledEvent(string EntityId, string EventId) : IEvent;

    public record EventThatFails(string EntityId, string EventId, Exception Exception) : IEvent;
    
    public record EventThatFailsOnce(string EntityId, string EventId, Exception Exception) : IEvent;

    public record TransformInto(string EntityId, string EventId, IImmutableList<object> Results) : IEvent;
    
    public interface IEvent
    {
        string EntityId { get; }
        string EventId { get; }
    }
}