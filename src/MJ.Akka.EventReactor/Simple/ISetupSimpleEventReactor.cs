using System.Collections.Immutable;

namespace MJ.Akka.EventReactor.Simple;

public interface ISetupSimpleEventReactor
{
    ISetupSimpleEventReactorFor<TEvent> On<TEvent>();

    internal IImmutableDictionary<Type, Func<IReactorContext, CancellationToken, Task<IImmutableList<object>>>> Build();
}

public interface ISetupSimpleEventReactorFor<out TEvent> 
    : ISetupSimpleEventReactor
{
    ISetupSimpleEventReactorFor<TEvent> HandleWith(
        Func<EventReactorContext, CancellationToken, Task<IImmutableList<object>>> handler);
}