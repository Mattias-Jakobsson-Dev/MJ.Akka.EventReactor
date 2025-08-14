using MJ.Akka.EventReactor.DeadLetter;

namespace MJ.Akka.EventReactor.Setup;

public interface IEventReactorProxy
{
    Task Stop();
    Task WaitForCompletion(TimeSpan? timeout = null);

    IDeadLetterManager GetDeadLetters();
}