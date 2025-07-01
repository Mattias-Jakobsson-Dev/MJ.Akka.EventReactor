using Akka;
using Akka.Actor;
using Akka.Streams.Dsl;

namespace MJ.Akka.EventReactor.PositionStreamSource;

public class PositionedStreamEventReactorEventSource(IStartPositionStream startPositionStream, ActorSystem actorSystem) 
    : IEventReactorEventSource
{
    public Source<IMessageWithAck, NotUsed> Start(IEventReactor reactor)
    {
        return Source.ActorPublisher<IMessageWithAck>(PositionedStreamWorker.Init(GetPublisherActorRef(reactor)))
            .MapMaterializedValue(_ => NotUsed.Instance);
    }
    
    protected virtual IActorRef GetPublisherActorRef(IEventReactor reactor)
    {
        return actorSystem.ActorOf(
            Props.Create(() => new PositionedStreamPublisher(reactor.Name, startPositionStream)));
    }
}