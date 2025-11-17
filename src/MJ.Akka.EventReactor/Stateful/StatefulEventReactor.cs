using System.Collections.Immutable;
using Akka.Actor;

namespace MJ.Akka.EventReactor.Stateful;

public abstract class StatefulEventReactor<TState>(ActorSystem actorSystem) : IConfigureEventReactor
{
    public abstract string Name { get; }

    public abstract Task<IEventReactorEventSource> GetSource();

    public IReactToEvent SetupReactor()
    {
        var results = Configure(new EventReactorSetup()).Build();
        
        return new ReactorToStatefulEvents<TState>(
            Name,
            results.ToImmutableDictionary(x => x.Key, x => x.Value.getId),
            results.ToImmutableDictionary(x => x.Key, x => x.Value.handle),
            GetDefaultState,
            CreateStorage(),
            actorSystem);
    }
    
    protected abstract ISetupStatefulEventReactor<TState> Configure(
        ISetupStatefulEventReactor<TState> config);

    protected abstract IStatefulReactorStorage CreateStorage();

    protected abstract TState? GetDefaultState(string? id);

    private class EventReactorSetup : ISetupStatefulEventReactor<TState>
    {
        private readonly Dictionary<Type, IEventReactorBuilder> _builders = new();

        public ISetupStatefulEventReactorFor<TEvent, TState> On<TEvent>(Func<TEvent, string> getId)
        {
            if (_builders.TryGetValue(typeof(TEvent), out var existingBuilder))
            {
                return (ISetupStatefulEventReactorFor<TEvent, TState>)existingBuilder;
            }

            var newBuilder = new EventReactorSetupFor<TEvent>(this, getId);

            _builders[typeof(TEvent)] = newBuilder;

            return newBuilder;
        }

        public IImmutableDictionary<
                Type, 
                (Func<object, string> getId, Func<IReactorContext, CancellationToken, Task<IImmutableList<object>>> handle)>
            Build()
        {
            return _builders
                .ToImmutableDictionary(
                    x => x.Key,
                    x => (
                        x.Value.CreateGetId(),
                        x.Value.CreateReactor()));
        }

        private class EventReactorSetupFor<TEvent>(
            ISetupStatefulEventReactor<TState> parent,
            Func<TEvent, string> getId)
            : ISetupStatefulEventReactorFor<TEvent, TState>, IEventReactorBuilder
        {
            private readonly
                List<Func<StatefulEventReactorContext<TState>, CancellationToken, Task<IImmutableList<object>>>>
                _handlers = [];

            public ISetupStatefulEventReactorFor<TEvent, TState> HandleWith(
                Func<StatefulEventReactorContext<TState>, CancellationToken, Task<IImmutableList<object>>> handler)
            {
                _handlers.Add(handler);

                return this;
            }

            public ISetupStatefulEventReactorFor<TNewEvent, TState> On<TNewEvent>(Func<TNewEvent, string> getIdFromEvent)
            {
                return parent.On(getIdFromEvent);
            }

            public IImmutableDictionary<
                    Type, 
                    (Func<object, string> getId, Func<IReactorContext, CancellationToken, Task<IImmutableList<object>>> handle)>
                Build()
            {
                return parent.Build();
            }

            public Func<object, string> CreateGetId()
            {
                return evnt => getId((TEvent)evnt);
            }

            public Func<IReactorContext, CancellationToken, Task<IImmutableList<object>>> CreateReactor()
            {
                return async (context, cancellationToken) =>
                {
                    var results = ImmutableList.CreateBuilder<object>();

                    foreach (var handler in _handlers)
                    {
                        var result = await handler((StatefulEventReactorContext<TState>)context, cancellationToken);

                        results.AddRange(result);
                    }

                    return results.ToImmutable();
                };
            }
        }

        private interface IEventReactorBuilder
        {
            Func<object, string> CreateGetId();
            Func<IReactorContext, CancellationToken, Task<IImmutableList<object>>> CreateReactor();
        }
    }
}