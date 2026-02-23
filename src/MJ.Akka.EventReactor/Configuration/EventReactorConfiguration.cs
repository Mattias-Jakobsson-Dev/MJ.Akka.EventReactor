using System.Collections.Immutable;
using Akka.Streams;

namespace MJ.Akka.EventReactor.Configuration;

public class EventReactorConfiguration(
    IConfigureEventReactor eventReactor,
    RestartSettings? restartSettings,
    int parallelism,
    TimeSpan timeout,
    IImmutableList<IOutputWriter> outputWriters,
    IReactToEvent handler)
{
    public string Name { get; } = eventReactor.Name;
    public TimeSpan Timeout { get; } = timeout;
    public int Parallelism { get; } = parallelism;
    public IImmutableList<IOutputWriter> OutputWriters { get; } = outputWriters;
    public RestartSettings? RestartSettings { get; } = restartSettings;

    public Task<IEventReactorEventSource> GetSource()
    {
        return eventReactor.GetSource();
    }
    
    public Task<IImmutableList<object>> Handle(IMessageWithAck msg, CancellationToken cancellationToken)
    {
        return handler.Handle(msg, cancellationToken);
    }
}