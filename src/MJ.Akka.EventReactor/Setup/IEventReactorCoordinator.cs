namespace MJ.Akka.EventReactor.Setup;

public interface IEventReactorCoordinator
{
    IEventReactorProxy? Get(string name);
}