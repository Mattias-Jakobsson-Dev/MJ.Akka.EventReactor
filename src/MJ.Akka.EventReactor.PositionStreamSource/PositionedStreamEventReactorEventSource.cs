using System.Collections.Immutable;
using Akka;
using Akka.Actor;
using Akka.Streams.Dsl;
using JetBrains.Annotations;
using MJ.Akka.EventReactor.DeadLetter;

namespace MJ.Akka.EventReactor.PositionStreamSource;

[PublicAPI]
public class PositionedStreamEventReactorEventSource : IEventReactorEventSourceWithDeadLetters
{
    private readonly IActorRef _publisher;
    
    public PositionedStreamEventReactorEventSource(
        IStartPositionStream startPositionStream,
        ActorSystem actorSystem,
        IEventReactor reactor,
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

    public IDeadLetterManager GetDeadLetters()
    {
        return new PublisherDeadLetterManager(_publisher);
    }
    
    private class GetPositionedStreamPublisher(
        ActorSystem actorSystem,
        IStartPositionStream startPositionStream,
        IEventReactor reactor,
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
    
    private class PublisherDeadLetterManager(IActorRef publisher) : IDeadLetterManager
    {
        public async Task<IImmutableList<DeadLetterData>> LoadDeadLetters()
        {
            var response = await publisher.Ask<PositionedStreamPublisher.Responses.GetDeadLettersResponse>(
                new PositionedStreamPublisher.Queries.GetDeadLetters());

            return response.DeadLetters;
        }

        public async Task Retry(long to)
        {
            var response = await publisher.Ask<PositionedStreamPublisher.Responses.RetryDeadLettersResponse>(
                new PositionedStreamPublisher.Commands.RetryDeadLetters(to));

            if (response.Error != null)
                throw response.Error;
        }

        public async Task Clear(long to)
        {
            var response = await publisher.Ask<PositionedStreamPublisher.Responses.ClearDeadLettersResponse>(
                new PositionedStreamPublisher.Commands.ClearDeadLetters(to));

            if (response.Error != null)
                throw response.Error;
        }
    }
}