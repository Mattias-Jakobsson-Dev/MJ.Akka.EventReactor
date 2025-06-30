namespace MJ.Akka.EventReactor.Setup;

public interface IEventReactorApplication
{
    Task<IEventReactorCoordinator> Start();
}