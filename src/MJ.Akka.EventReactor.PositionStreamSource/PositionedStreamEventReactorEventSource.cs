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
        bool useDeadLetter = true,
        int maxRetries = 5)
        : this(
            startPositionStream,
            actorSystem,
            reactor.Name,
            parallelism,
            positionBatchSize,
            positionWriteInterval,
            useDeadLetter,
            maxRetries)
    {
        
    }
    
    public PositionedStreamEventReactorEventSource(
        IStartPositionStream startPositionStream,
        ActorSystem actorSystem,
        string reactorName,
        int parallelism = 100,
        int positionBatchSize = 1_000,
        TimeSpan? positionWriteInterval = null,
        bool useDeadLetter = true,
        int maxRetries = 5)
        : this(new GetPositionedStreamPublisher(
            actorSystem,
            startPositionStream,
            reactorName,
            parallelism,
            positionBatchSize,
            positionWriteInterval ?? TimeSpan.FromSeconds(5),
            useDeadLetter,
            maxRetries))
    {
        
    }
    
    protected PositionedStreamEventReactorEventSource(
        IGetPositionedStreamPublisher positionedStreamPublisher)
    {
        _publisher = positionedStreamPublisher.GetPublisherActorRef();
    }
    
    public Source<IMessageWithAck, NotUsed> Start(CancellationToken cancellationToken)
    {
        _publisher.Tell(new PositionedStreamPublisher.Commands.Start());
        
        return Source.ActorPublisher<IMessageWithAck>(PositionedStreamWorker.Init(_publisher))
            .MapMaterializedValue(_ => NotUsed.Instance);
    }
    
    private class GetPositionedStreamPublisher(
        ActorSystem actorSystem,
        IStartPositionStream startPositionStream,
        string reactorName,
        int parallelism,
        int positionBatchSize,
        TimeSpan positionWriteInterval,
        bool useDeadLetter,
        int maxRetries) : IGetPositionedStreamPublisher
    {
        public IActorRef GetPublisherActorRef()
        {
            var settings = new PositionedStreamSettings(parallelism, positionBatchSize, positionWriteInterval, useDeadLetter, maxRetries);
            return actorSystem.ActorOf(
                Props.Create(() => new PositionedStreamPublisher(
                    reactorName,
                    startPositionStream,
                    settings)));
        }
    }
}