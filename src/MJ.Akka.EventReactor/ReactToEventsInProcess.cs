using System.Collections.Immutable;

namespace MJ.Akka.EventReactor;

public class ReactToEventsInProcess(
    IImmutableDictionary<Type, Func<object, CancellationToken, Task<IImmutableList<object>>>> handlers) : IReactToEvent
{
    public async Task<IImmutableList<object>> Handle(object evnt, CancellationToken cancellationToken)
    {
        var typesToCheck = evnt.GetType().GetInheritedTypes();
        
        var results = ImmutableList.CreateBuilder<object>();

        foreach (var type in typesToCheck)
        {
            if (!handlers.TryGetValue(type, out var handler))
                continue;

            results.AddRange(await handler(evnt, cancellationToken));
        }

        return results.ToImmutable();
    }
}