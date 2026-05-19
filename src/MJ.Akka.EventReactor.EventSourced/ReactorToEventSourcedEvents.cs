using System.Collections.Immutable;
using Akka.Actor;
using Akka.Event;
using Akka.Persistence;
using Akka.Util;

namespace MJ.Akka.EventReactor.EventSourced;

public class ReactorToEventSourcedEvents<TState>(
    string reactorName,
    IImmutableDictionary<Type, Func<object, string>> getIds,
    IImmutableDictionary<Type, Func<IReactorContext, CancellationToken, Task<IImmutableList<object>>>> handlers,
    IImmutableDictionary<Type, Func<TState, object, TState>> eventAppliers,
    Func<string, TState> getDefaultState,
    ActorSystem actorSystem) : IReactToEvent
{
    private readonly IActorRef _handler = actorSystem.ActorOf(
        Props.Create(() => new SequentialReactorMessageHandlerCoordinator(
            reactorName,
            handlers,
            eventAppliers,
            getDefaultState)));

    public async Task<IImmutableList<object>> Handle(IMessageWithAck msg, CancellationToken cancellationToken)
    {
        var typesToCheck = GetTypesToCheck(msg.Message.GetType());

        var id = (from type in typesToCheck
                where handlers.ContainsKey(type)
                let getId = getIds[type]
                select getId(msg.Message))
            .FirstOrDefault();
        
        if (string.IsNullOrEmpty(id))
            return ImmutableList<object>.Empty;

        var response = await _handler
            .Ask<SequentialReactorMessageHandler.Responses.HandleResponse>(
                new SequentialReactorMessageHandlerCoordinator.Commands.SendToHandler(
                    id,
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
        private readonly IImmutableDictionary<Type, Func<TState, object, TState>> _eventAppliers;
        private readonly Func<string, TState> _getDefaultState;

        public static class Commands
        {
            public record SendToHandler(string Id, object Message);
        }

        public SequentialReactorMessageHandlerCoordinator(
            string reactorName,
            IImmutableDictionary<Type, Func<IReactorContext, CancellationToken, Task<IImmutableList<object>>>> handlers,
            IImmutableDictionary<Type, Func<TState, object, TState>> eventAppliers,
            Func<string, TState> getDefaultState)
        {
            _handlers = handlers;
            _eventAppliers = eventAppliers;
            _getDefaultState = getDefaultState;

            Receive<Commands.SendToHandler>(cmd =>
            {
                var handler = GetHandler(reactorName, cmd.Id);

                handler.Tell(cmd.Message, Sender);
            });
        }

        private IActorRef GetHandler(string reactorName, string id)
        {
            var actorName = MurmurHash.StringHash(id).ToString();

            return Context.Child(actorName).GetOrElse(() => Context.ActorOf(
                Props.Create(() => new SequentialReactorMessageHandler(
                    reactorName,
                    id,
                    _handlers,
                    _eventAppliers,
                    _getDefaultState)), actorName));
        }
    }

    private class SequentialReactorMessageHandler : ReceivePersistentActor
    {
        private readonly IImmutableDictionary<Type, Func<TState, object, TState>> _eventAppliers;
        private TState _state;

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
            IImmutableDictionary<Type, Func<TState, object, TState>> eventAppliers,
            Func<string, TState> getDefaultState)
        {
            var handlers1 = handlers;
            _eventAppliers = eventAppliers;
            _state = getDefaultState(id);
            
            var logger = Context.GetLogger();

            PersistenceId = $"event-reactor-{reactorName}-{id}";

            Recover<object>(ApplyPersistedEvent);

            CommandAsync<Commands.Handle>(async cmd =>
            {
                try
                {
                    var results = ImmutableList.CreateBuilder<object>();

                    var context = new EventSourcedEventReactorContext<TState>(
                        cmd.Message.Message,
                        cmd.Message.Metadata,
                        _state);

                    foreach (var type in cmd.Types)
                    {
                        if (!handlers1.TryGetValue(type, out var handler))
                            continue;

                        results.AddRange(await handler(context, cmd.CancellationToken));
                    }

                    var persistedEvents = results.ToImmutable();

                    if (persistedEvents.Count == 0)
                    {
                        Sender.Tell(new Responses.HandleResponse(persistedEvents));
                        return;
                    }

                    var originalSender = Sender;
                    var remaining = persistedEvents.Count;

                    PersistAll(persistedEvents, evnt =>
                    {
                        ApplyPersistedEvent(evnt);

                        remaining--;

                        if (remaining == 0)
                        {
                            originalSender.Tell(new Responses.HandleResponse(persistedEvents));
                        }
                    });
                }
                catch (Exception e)
                {
                    logger.Error(e,
                        "[EventSourced] Error in reactor '{0}' | PersistenceId={1} | EventType={2} | Error: {3}",
                        reactorName,
                        PersistenceId,
                        cmd.Message.Message.GetType().FullName,
                        e.Message);

                    Sender.Tell(new Responses.HandleResponse(ImmutableList<object>.Empty, e));
                }
            });
        }

        public override string PersistenceId { get; }

        private void ApplyPersistedEvent(object evnt)
        {
            foreach (var type in GetTypesToCheck(evnt.GetType()))
            {
                if (!_eventAppliers.TryGetValue(type, out var applier))
                    continue;

                _state = applier(_state, evnt);
                break;
            }
        }
    }

    private static IImmutableList<Type> GetTypesToCheck(Type type)
    {
        var result = new List<Type>();

        var currentType = type;

        while (currentType != null)
        {
            result.Add(currentType);
            currentType = currentType.BaseType;
        }

        result.AddRange(type.GetInterfaces());

        return result.ToImmutableList();
    }
}
