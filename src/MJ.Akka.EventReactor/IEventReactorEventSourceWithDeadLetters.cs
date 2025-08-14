using MJ.Akka.EventReactor.DeadLetter;

namespace MJ.Akka.EventReactor;

public interface IEventReactorEventSourceWithDeadLetters : IEventReactorEventSource
{
    IDeadLetterManager GetDeadLetters();
}