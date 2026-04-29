using System.Collections.Immutable;
using JetBrains.Annotations;

namespace MJ.Akka.EventReactor.EventSourced;

[PublicAPI]
public class EventSourcedEventReactorContext<TState>(
    object evnt,
    IImmutableDictionary<string, object?> metadata,
    TState state) : IReactorContext
{
    public object Event { get; } = evnt;
    public IImmutableDictionary<string, object?> Metadata { get; } = metadata;
    public TState State { get; private set; } = state;
    internal bool HasModifiedState { get; private set; }

    public object GetEvent()
    {
        return Event;
    }
}