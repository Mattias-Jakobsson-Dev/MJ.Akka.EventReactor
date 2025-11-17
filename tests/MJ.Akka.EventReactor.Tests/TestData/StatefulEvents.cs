namespace MJ.Akka.EventReactor.Tests.TestData;

public static class StatefulEvents
{
    public record EventThatModifiesState(string EntityId, string EventId, Func<TestState?, Task<TestState?>> Modify) 
        : Events.IEvent;
}