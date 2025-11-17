using System.Collections.Immutable;

namespace MJ.Akka.EventReactor;

public interface IReactToEvent
{
    Task<IImmutableList<object>> Handle(IMessageWithAck msg, CancellationToken cancellationToken);
}