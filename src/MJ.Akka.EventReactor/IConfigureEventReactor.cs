namespace MJ.Akka.EventReactor;

public interface IConfigureEventReactor
{
    string Name { get; }
    
    Task<IEventReactorEventSource> GetSource();

    IReactToEvent SetupReactor();
}