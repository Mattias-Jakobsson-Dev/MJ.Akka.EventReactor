namespace MJ.Akka.EventReactor;

public interface IEventReactor
{
    string Name { get; }

    ISetupEventReactor Configure(ISetupEventReactor config);

    IEventReactorEventSource GetSource();
}