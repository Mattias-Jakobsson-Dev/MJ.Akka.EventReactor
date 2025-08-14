using Akka.Actor;

namespace MJ.Akka.EventReactor.PositionStreamSource;

public interface IGetPositionedStreamPublisher
{
    IActorRef GetPublisherActorRef();
}