using System.Collections.Immutable;

namespace MJ.Akka.EventReactor;

public interface IMessageWithAck
{
    object Message { get; }
    IImmutableDictionary<string, object?> Metadata { get; }
    
    Task Ack(CancellationToken cancellationToken);
    Task Nack(Exception error, CancellationToken cancellationToken);
}