using Akka.Streams;

namespace MJ.Akka.EventReactor.Configuration;

public class EventReactorConfiguration(
    IEventReactor eventReactor,
    RestartSettings? restartSettings,
    int parallelism,
    IReactToEvent handler)
{
    public string Name { get; } = eventReactor.Name;
    public int Parallelism { get; } = parallelism;
    public RestartSettings? RestartSettings { get; } = restartSettings;

    public Task<IEventReactorEventSource> GetSource()
    {
        return eventReactor.GetSource();
    }
    
    public Task Handle(object evnt, CancellationToken cancellationToken)
    {
        return handler.Handle(evnt, cancellationToken);
    }
}