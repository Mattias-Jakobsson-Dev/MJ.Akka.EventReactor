namespace DC.Akka.EventReactor.Setup;

public interface IEventReactorApplication
{
    Task<IEventReactorCoordinator> Start();
}