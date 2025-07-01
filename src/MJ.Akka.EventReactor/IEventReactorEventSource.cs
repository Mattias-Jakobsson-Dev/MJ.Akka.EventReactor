using Akka;
using Akka.Streams.Dsl;

namespace MJ.Akka.EventReactor;

public interface IEventReactorEventSource
{
    Source<IMessageWithAck, NotUsed> Start();
}