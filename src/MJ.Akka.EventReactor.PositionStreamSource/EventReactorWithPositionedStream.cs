using Akka;
using Akka.Actor;
using Akka.Streams.Dsl;

namespace MJ.Akka.EventReactor.PositionStreamSource;

public abstract class EventReactorWithPositionedStream(ActorSystem actorSystem) : IEventReactor
{
    public abstract string Name { get; }

    public abstract ISetupEventReactor Configure(ISetupEventReactor config);

    public Source<IMessageWithAck, NotUsed> StartSource()
    {
        return Source.ActorPublisher<IMessageWithAck>(PositionedStreamWorker.Init(GetPublisherActorRef()))
            .MapMaterializedValue(_ => NotUsed.Instance);
    }

    protected abstract IStartPositionStream GetStreamSource();

    protected virtual IActorRef GetPublisherActorRef()
    {
        return actorSystem.ActorOf(
            Props.Create(() => new PositionedStreamPublisher(Name, GetStreamSource())));
    }
}