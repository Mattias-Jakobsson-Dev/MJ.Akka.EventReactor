using Akka;
using Akka.Streams;
using Akka.Streams.Dsl;

namespace MJ.Akka.EventReactor.Configuration;

public class EventReactorConfiguration(
    IEventReactor eventReactor,
    RestartSettings? restartSettings,
    IReactToEvent handler)
{
    public string Name { get; } = eventReactor.Name;
    public RestartSettings? RestartSettings { get; } = restartSettings;

    public Source<IMessageWithAck, NotUsed> StartSource()
    {
        return eventReactor.GetSource().Start(eventReactor);
    }
    
    public Task Handle(object evnt, CancellationToken cancellationToken)
    {
        return handler.Handle(evnt, cancellationToken);
    }
}