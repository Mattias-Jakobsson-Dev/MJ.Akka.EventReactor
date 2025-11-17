using System.Collections.Immutable;
using Akka.Actor;

namespace MJ.Akka.EventReactor.Stateful;

public class ReactorToStatefulEvents<TState>(
    string reactorName,
    IImmutableDictionary<Type, Func<object, string>> getIds,
    IImmutableDictionary<Type, Func<IReactorContext, CancellationToken, Task<IImmutableList<object>>>> handlers,
    Func<string?, TState?> getDefaultState,
    IStatefulReactorStorage storage,
    ActorSystem actorSystem) : IReactToEvent
{
    private readonly IActorRef _handler = actorSystem.ActorOf(
        Props.Create(() => new SequentialReactorMessageHandlerCoordinator(
            reactorName,
            handlers,
            getDefaultState,
            storage)));
    
    public async Task<IImmutableList<object>> Handle(IMessageWithAck msg, CancellationToken cancellationToken)
    {
        var typesToCheck = msg.Message.GetType().GetInheritedTypes();
        
        var id = (from type in typesToCheck
                where handlers.ContainsKey(type)
                let getId = getIds[type]
                select getId(msg.Message))
            .FirstOrDefault();

        var response = await _handler
            .Ask<SequentialReactorMessageHandler.Responses.HandleResponse>(
                new SequentialReactorMessageHandlerCoordinator.Commands.SendToHandler(
                    id ?? "default",
                    new SequentialReactorMessageHandler.Commands.Handle(
                        typesToCheck,
                        msg,
                        cancellationToken)),
                cancellationToken: cancellationToken);

        return response.Exception == null 
            ? response.Results 
            : throw response.Exception;
    }
    
    private class SequentialReactorMessageHandlerCoordinator : ReceiveActor
    {
        private readonly IImmutableDictionary<
            Type,
            Func<IReactorContext, CancellationToken, Task<IImmutableList<object>>>> _handlers;
        private readonly Func<string?, TState?> _getDefaultState;
        private readonly IStatefulReactorStorage _storage;

        public static class Commands
        {
            public record SendToHandler(string Id, object Message);
        }

        public SequentialReactorMessageHandlerCoordinator(
            string reactorName,
            IImmutableDictionary<Type, Func<IReactorContext, CancellationToken, Task<IImmutableList<object>>>> handlers,
            Func<string?, TState?> getDefaultState,
            IStatefulReactorStorage storage)
        {
            _handlers = handlers;
            _getDefaultState = getDefaultState;
            _storage = storage;

            Receive<Commands.SendToHandler>(cmd =>
            {
                var handler = GetHandler(reactorName, cmd.Id);

                handler.Tell(cmd.Message, Sender);
            });
        }

        private IActorRef GetHandler(string reactorName, string id)
        {
            return Context.Child(id).GetOrElse(() => Context.ActorOf(
                Props.Create(() => new SequentialReactorMessageHandler(
                    reactorName,
                    id,
                    _handlers,
                    _getDefaultState,
                    _storage)), id));
        }
    }
    
    private class SequentialReactorMessageHandler : ReceiveActor
    {
        public static class Commands
        {
            public record Handle(
                IImmutableList<Type> Types,
                IMessageWithAck Message,
                CancellationToken CancellationToken);
        }
        
        public static class Responses
        {
            public record HandleResponse(IImmutableList<object> Results, Exception? Exception = null);
        }

        public SequentialReactorMessageHandler(
            string reactorName,
            string id,
            IImmutableDictionary<Type, Func<IReactorContext, CancellationToken, Task<IImmutableList<object>>>> handlers,
            Func<string?, TState?> getDefaultState,
            IStatefulReactorStorage storage)
        {
            ReceiveAsync<Commands.Handle>(async cmd =>
            {
                try
                {
                    var state = !string.IsNullOrEmpty(id)
                        ? await storage.Load<TState>(reactorName, id, cmd.CancellationToken) ?? getDefaultState(id)
                        : getDefaultState(id);

                    var results = ImmutableList.CreateBuilder<object>();

                    var context = new StatefulEventReactorContext<TState>(
                        cmd.Message.Message,
                        cmd.Message.Metadata,
                        state);

                    foreach (var type in cmd.Types)
                    {
                        if (!handlers.TryGetValue(type, out var handler))
                            continue;

                        results.AddRange(await handler(context, cmd.CancellationToken));
                    }

                    if (!string.IsNullOrEmpty(id) && context.HasModifiedState)
                    {
                        if (context.State == null)
                            await storage.Delete(reactorName, id, cmd.CancellationToken);
                        else
                            await storage.Save(reactorName, id, context.State, cmd.CancellationToken);
                    }

                    Sender.Tell(new Responses.HandleResponse(results.ToImmutable()));
                }
                catch (Exception e)
                {
                    Sender.Tell(new Responses.HandleResponse(ImmutableList<object>.Empty, e));
                }
            });
        }
    }
}