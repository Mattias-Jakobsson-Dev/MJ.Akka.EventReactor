namespace MJ.Akka.EventReactor;

public interface IMessageWithAck
{
    object Message { get; }
    
    Task Ack();
    Task Nack(Exception error);
}