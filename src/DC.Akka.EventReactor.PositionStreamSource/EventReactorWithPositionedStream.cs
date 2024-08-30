using Akka;
using Akka.Streams.Dsl;

namespace DC.Akka.EventReactor.PositionStreamSource;

public abstract class EventReactorWithPositionedStream : IEventReactor
{
    public abstract string Name { get; }

    public abstract void Configure(Func<ISetupEventReactor, ISetupEventReactor> config);

    public Source<IMessageWithAck, NotUsed> StartSource()
    {
        return Source.ActorPublisher<IMessageWithAck>(PositionedStreamPublished.Init(GetStreamSource(), Name))
            .MapMaterializedValue(_ => NotUsed.Instance);
    }

    protected abstract IStartPositionStream GetStreamSource();
}