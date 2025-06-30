using System.Collections.Immutable;

namespace MJ.Akka.EventReactor;

public class ReactToEventsInProcess(
    IImmutableDictionary<Type, Func<object, CancellationToken, Task>> handlers) : IReactToEvent
{
    public async Task Handle(object evnt, CancellationToken cancellationToken)
    {
        var typesToCheck = evnt.GetType().GetInheritedTypes();

        foreach (var type in typesToCheck)
        {
            if (!handlers.TryGetValue(type, out var handler))
                continue;

            await handler(evnt, cancellationToken);
        }
    }
}