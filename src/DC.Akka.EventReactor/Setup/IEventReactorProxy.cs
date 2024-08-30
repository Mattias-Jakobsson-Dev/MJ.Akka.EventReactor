namespace DC.Akka.EventReactor.Setup;

public interface IEventReactorProxy
{
    IEventReactor EventReactor { get; }

    Task Stop();
    Task WaitForCompletion(TimeSpan? timeout = null);
}