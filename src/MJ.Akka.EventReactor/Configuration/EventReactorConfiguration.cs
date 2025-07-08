using System.Collections.Immutable;
using Akka.Streams;

namespace MJ.Akka.EventReactor.Configuration;

public class EventReactorConfiguration(
    IEventReactor eventReactor,
    RestartSettings? restartSettings,
    int parallelism,
    IImmutableList<IOutputWriter> outputWriters,
    IReactToEvent handler)
{
    public string Name { get; } = eventReactor.Name;
    public int Parallelism { get; } = parallelism;
    public IImmutableList<IOutputWriter> OutputWriters { get; } = outputWriters;
    public RestartSettings? RestartSettings { get; } = restartSettings;

    public Task<IEventReactorEventSource> GetSource()
    {
        return eventReactor.GetSource();
    }
    
    public Task<IImmutableList<object>> Handle(object evnt, CancellationToken cancellationToken)
    {
        return handler.Handle(evnt, cancellationToken);
    }
}