using Akka;
using Akka.Streams.Dsl;

namespace MJ.Akka.EventReactor.PositionStreamSource;

public interface IStartPositionStream
{
    Source<EventWithPosition, NotUsed> StartFrom(long? position);
    Task<long?> GetInitialPosition();
}