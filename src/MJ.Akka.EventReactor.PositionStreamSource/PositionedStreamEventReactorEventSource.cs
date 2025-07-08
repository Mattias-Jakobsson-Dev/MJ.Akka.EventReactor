using Akka;
using Akka.Actor;
using Akka.Streams.Dsl;
using JetBrains.Annotations;

namespace MJ.Akka.EventReactor.PositionStreamSource;

[PublicAPI]
public class PositionedStreamEventReactorEventSource(
    IStartPositionStream startPositionStream,
    ActorSystem actorSystem,
    IEventReactor reactor,
    int parallelism = 100)
    : IEventReactorEventSource
{
    public Source<IMessageWithAck, NotUsed> Start()
    {
        return Source.ActorPublisher<IMessageWithAck>(PositionedStreamWorker.Init(GetPublisherActorRef()))
            .MapMaterializedValue(_ => NotUsed.Instance);
    }
    
    protected virtual IActorRef GetPublisherActorRef()
    {
        return actorSystem.ActorOf(
            Props.Create(() => new PositionedStreamPublisher(reactor.Name, startPositionStream, parallelism)));
    }
}