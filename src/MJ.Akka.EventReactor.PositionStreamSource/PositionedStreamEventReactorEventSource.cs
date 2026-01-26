using Akka;
using Akka.Actor;
using Akka.Streams.Dsl;
using JetBrains.Annotations;

namespace MJ.Akka.EventReactor.PositionStreamSource;

[PublicAPI]
public class PositionedStreamEventReactorEventSource : IEventReactorEventSource
{
    private readonly IActorRef _publisher;
    
    public PositionedStreamEventReactorEventSource(
        IStartPositionStream startPositionStream,
        ActorSystem actorSystem,
        IConfigureEventReactor reactor,
        int parallelism = 100,
        int positionBatchSize = 100,
        TimeSpan? positionWriteInterval = null,
        TimeSpan? messageTimeout = null)
        : this(new GetPositionedStreamPublisher(
            actorSystem,
            startPositionStream,
            reactor,
            parallelism,
            positionBatchSize,
            positionWriteInterval ?? TimeSpan.FromSeconds(5),
            messageTimeout ?? TimeSpan.FromSeconds(10)))
    {
        
    }
    
    protected PositionedStreamEventReactorEventSource(
        IGetPositionedStreamPublisher positionedStreamPublisher)
    {
        _publisher = positionedStreamPublisher.GetPublisherActorRef();
    }
    
    public Source<IMessageWithAck, NotUsed> Start()
    {
        _publisher.Tell(new PositionedStreamPublisher.Commands.Start());
        
        return Source.ActorPublisher<IMessageWithAck>(PositionedStreamWorker.Init(_publisher))
            .MapMaterializedValue(_ => NotUsed.Instance);
    }
    
    private class GetPositionedStreamPublisher(
        ActorSystem actorSystem,
        IStartPositionStream startPositionStream,
        IConfigureEventReactor reactor,
        int parallelism,
        int positionBatchSize,
        TimeSpan positionWriteInterval,
        TimeSpan messageTimeout) : IGetPositionedStreamPublisher
    {
        public IActorRef GetPublisherActorRef()
        {
            return actorSystem.ActorOf(
                Props.Create(() => new PositionedStreamPublisher(
                    reactor.Name,
                    startPositionStream, 
                    new PositionedStreamSettings(parallelism, positionBatchSize, positionWriteInterval, messageTimeout))));
        }
    }
}