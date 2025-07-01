using Akka;
using Akka.Actor;
using Akka.Streams.Dsl;

namespace MJ.Akka.EventReactor.PositionStreamSource;

public class PositionedStreamEventReactorEventSource(
    IStartPositionStream startPositionStream,
    ActorSystem actorSystem,
    IEventReactor reactor) 
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
            Props.Create(() => new PositionedStreamPublisher(reactor.Name, startPositionStream)));
    }
}