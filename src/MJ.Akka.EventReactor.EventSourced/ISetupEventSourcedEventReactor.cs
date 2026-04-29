using System.Collections.Immutable;

namespace MJ.Akka.EventReactor.EventSourced;

public interface ISetupEventSourcedEventReactor<TState>
{
    ISetupEventSourcedEventReactorFor<TEvent, TState> On<TEvent>(Func<TEvent, string> getId);

    internal IImmutableDictionary<
        Type,
        (Func<object, string> getId, Func<IReactorContext, CancellationToken, Task<IImmutableList<object>>> handle)> Build();
}

public interface ISetupEventSourcedEventReactorFor<out TEvent, TState> 
    : ISetupEventSourcedEventReactor<TState>
{
    ISetupEventSourcedEventReactorFor<TEvent, TState> HandleWith(
        Func<EventSourcedEventReactorContext<TState>, CancellationToken, Task<IImmutableList<object>>> handler);
}