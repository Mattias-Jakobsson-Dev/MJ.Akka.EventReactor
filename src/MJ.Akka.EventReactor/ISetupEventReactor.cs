using System.Collections.Immutable;

namespace MJ.Akka.EventReactor;

public interface ISetupEventReactor
{
    ISetupEventReactor On<TEvent>(Action<TEvent> handler);

    ISetupEventReactor On<TEvent>(Func<TEvent, Task> handler);

    ISetupEventReactor On<TEvent>(Func<TEvent, CancellationToken, Task> handler);

    internal IImmutableDictionary<Type, Func<object, CancellationToken, Task>> Build();
}