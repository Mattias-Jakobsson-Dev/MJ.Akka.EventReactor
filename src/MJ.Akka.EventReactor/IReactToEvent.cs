using System.Collections.Immutable;

namespace MJ.Akka.EventReactor;

public interface IReactToEvent
{
    Task<IImmutableList<object>> Handle(object evnt, CancellationToken cancellationToken);
}