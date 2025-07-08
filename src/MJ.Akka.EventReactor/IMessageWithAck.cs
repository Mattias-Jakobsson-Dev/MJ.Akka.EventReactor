namespace MJ.Akka.EventReactor;

public interface IMessageWithAck
{
    object Message { get; }
    
    Task Ack(CancellationToken cancellationToken);
    Task Nack(Exception error, CancellationToken cancellationToken);
}