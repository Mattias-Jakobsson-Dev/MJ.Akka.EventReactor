using Akka;
using Akka.Streams.Dsl;
using JetBrains.Annotations;

namespace MJ.Akka.EventReactor;

[PublicAPI]
public class MultiSourceEventReactorEventSource(params IEventReactorEventSource[] sources)  : IEventReactorEventSource
{
    public Source<IMessageWithAck, NotUsed> Start()
    {
        return sources.Length switch
        {
            0 => Source.Empty<IMessageWithAck>(),
            1 => sources[0].Start(),
            _ => Source.Combine(sources[0].Start(), sources[1].Start(), i => new Merge<IMessageWithAck>(i),
                sources.Skip(2).Select(x => x.Start()).ToArray())
        };
    }
}