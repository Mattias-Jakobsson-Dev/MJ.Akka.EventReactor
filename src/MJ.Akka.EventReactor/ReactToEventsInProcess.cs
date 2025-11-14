using System.Collections.Immutable;

namespace MJ.Akka.EventReactor;

public class ReactToEventsInProcess(
    IImmutableDictionary<Type, Func<IReactorContext, CancellationToken, Task<IImmutableList<object>>>> handlers) 
    : IReactToEvent
{
    public async Task<IImmutableList<object>> Handle(IMessageWithAck msg, CancellationToken cancellationToken)
    {
        var typesToCheck = msg.Message.GetType().GetInheritedTypes();
        
        var results = ImmutableList.CreateBuilder<object>();

        var context = new EventReactorContext(msg.Message, msg.Metadata);

        foreach (var type in typesToCheck)
        {
            if (!handlers.TryGetValue(type, out var handler))
                continue;

            results.AddRange(await handler(context, cancellationToken));
        }

        return results.ToImmutable();
    }
}