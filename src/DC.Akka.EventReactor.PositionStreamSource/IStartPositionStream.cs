using Akka;
using Akka.Streams.Dsl;

namespace DC.Akka.EventReactor.PositionStreamSource;

public interface IStartPositionStream
{
    Source<EventWithPosition, NotUsed> StartFrom(long? position);
}