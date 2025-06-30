namespace MJ.Akka.EventReactor.Setup;

public interface IEventReactorProxy
{
    IEventReactor EventReactor { get; }

    Task Stop();
    Task WaitForCompletion(TimeSpan? timeout = null);
}