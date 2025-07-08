using System.Collections.Immutable;

namespace MJ.Akka.EventReactor.Setup;

public interface ISetupEventReactor
{
    ISetupEventReactorFor<TEvent> On<TEvent>();

    internal IImmutableDictionary<Type, Func<object, CancellationToken, Task<IImmutableList<object>>>> Build();
}

public interface ISetupEventReactorFor<out TEvent> : ISetupEventReactor
{
    ISetupEventReactorFor<TEvent> HandleWith(Func<TEvent, CancellationToken, Task<IImmutableList<object>>> handler);
}