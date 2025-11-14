using System.Collections.Immutable;
using JetBrains.Annotations;

namespace MJ.Akka.EventReactor.Stateful;

[PublicAPI]
public class StatefulEventReactorContext<TState>(
    object evnt,
    IImmutableDictionary<string, object?> metadata,
    TState? state) : IReactorContext
{
    public object Event { get; } = evnt;
    public IImmutableDictionary<string, object?> Metadata { get; } = metadata;
    public TState? State { get; private set; } = state;

    public object GetEvent()
    {
        return Event;
    }

    internal void ModifyState(TState? newState)
    {
        State = newState;
    }
}