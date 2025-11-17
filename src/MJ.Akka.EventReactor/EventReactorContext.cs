using System.Collections.Immutable;
using JetBrains.Annotations;

namespace MJ.Akka.EventReactor;

[PublicAPI]
public record EventReactorContext(object Event, IImmutableDictionary<string, object?> Metadata) : IReactorContext
{
    public object GetEvent()
    {
        return Event;
    }
}