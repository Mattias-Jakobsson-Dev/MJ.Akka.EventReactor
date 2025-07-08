using Akka.Actor;
using Akka.Streams.Actors;

namespace MJ.Akka.EventReactor.PositionStreamSource;

public class PositionedStreamWorker(IActorRef publisher) : ActorPublisher<IMessageWithAck>
{
    protected override bool Receive(object message)
    {
        switch (message)
        {
            case Request req:
                publisher.Tell(new PositionedStreamPublisher.Commands.Request(req.Count));

                return true;
            
            case PositionedStreamPublisher.Responses.SuccessRequestResponse success:
                foreach (var evnt in success.EventsToHandle)
                    OnNext(new PositionedEventWithAck(evnt, publisher));

                return true;
            
            case PositionedStreamPublisher.Responses.CompletedRequestResponse:
                OnComplete();

                return true;
            
            case PositionedStreamPublisher.Responses.FailureRequestResponse failure:
                OnError(failure.Failure);
                
                return true;

            case Cancel:
                publisher.Tell(new PositionedStreamPublisher.Commands.CancelDemand());

                return true;
        }

        return false;
    }

    public static Props Init(IActorRef publisher)
    {
        return Props.Create(() => new PositionedStreamWorker(publisher));
    }

    private record PositionedEventWithAck(EventWithPosition Event, IActorRef AckTo) : IMessageWithAck
    {
        public object Message => Event.Event;

        public Task Ack()
        {
            return AckTo.Ask<PositionedStreamPublisher.Responses.AckNackResponse>(
                new PositionedStreamPublisher.Commands.Ack(Event.Position));
        }

        public Task Nack(Exception error)
        {
            return AckTo.Ask<PositionedStreamPublisher.Responses.AckNackResponse>(
                new PositionedStreamPublisher.Commands.Nack(Event.Position, error));
        }
    }
}