using System.Collections.Immutable;

namespace MJ.Akka.EventReactor.Stateful;

public class ReactorToStatefulEvents<TState>(
    IImmutableDictionary<Type, Func<object, string>> getIds,
    IImmutableDictionary<Type, Func<IReactorContext, CancellationToken, Task<IImmutableList<object>>>> handlers,
    Func<string?, TState?> getDefaultState,
    IStatefulReactorStorage storage) : IReactToEvent
{
    public async Task<IImmutableList<object>> Handle(IMessageWithAck msg, CancellationToken cancellationToken)
    {
        var typesToCheck = msg.Message.GetType().GetInheritedTypes();
        
        var id = (from type in typesToCheck
                where handlers.ContainsKey(type)
                let getId = getIds[type]
                select getId(msg.Message))
            .FirstOrDefault();

        var state = !string.IsNullOrEmpty(id)
            ? await storage.Load<TState>(id, cancellationToken) ?? getDefaultState(id)
            : getDefaultState(id);
        
        var results = ImmutableList.CreateBuilder<object>();

        var context = new StatefulEventReactorContext<TState>(
            msg.Message,
            msg.Metadata,
            state);
        
        foreach (var type in typesToCheck)
        {
            if (!handlers.TryGetValue(type, out var handler))
                continue;

            results.AddRange(await handler(context, cancellationToken));
        }

        if (string.IsNullOrEmpty(id)) 
            return results.ToImmutable();
        
        if (context.State == null)
            await storage.Delete(id, cancellationToken);
        else
            await storage.Save(id, context.State, cancellationToken);

        return results.ToImmutable();
    }
}