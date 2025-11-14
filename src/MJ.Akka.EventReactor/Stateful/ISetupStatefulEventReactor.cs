using System.Collections.Immutable;

namespace MJ.Akka.EventReactor.Stateful;

public interface ISetupStatefulEventReactor<TState>
{
    ISetupStatefulEventReactorFor<TEvent, TState> On<TEvent>(Func<TEvent, string> getId);

    internal IImmutableDictionary<
        Type,
        (Func<object, string> getId, Func<IReactorContext, CancellationToken, Task<IImmutableList<object>>> handle)> Build();
}

public interface ISetupStatefulEventReactorFor<out TEvent, TState> 
    : ISetupStatefulEventReactor<TState>
{
    ISetupStatefulEventReactorFor<TEvent, TState> HandleWith(
        Func<StatefulEventReactorContext<TState>, CancellationToken, Task<IImmutableList<object>>> handler);
}