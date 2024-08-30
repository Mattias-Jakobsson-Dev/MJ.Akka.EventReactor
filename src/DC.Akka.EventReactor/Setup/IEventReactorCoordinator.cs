namespace DC.Akka.EventReactor.Setup;

public interface IEventReactorCoordinator
{
    IEventReactorProxy? Get(string name);
}