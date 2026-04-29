using System.Collections.Immutable;
using Akka.Actor;

namespace MJ.Akka.EventReactor.EventSourced;

public abstract class EventSourcedEventReactor<TState>(ActorSystem actorSystem) : IConfigureEventReactor
{
    public abstract string Name { get; }

    public abstract Task<IEventReactorEventSource> GetSource();

    public IReactToEvent SetupReactor()
    {
        var setupResults = Configure(new EventReactorSetup()).Build();

        var appliers = new EventApplierSetup();

        SetupEventAppliers(appliers);

        return new ReactorToEventSourcedEvents<TState>(
            Name,
            setupResults.ToImmutableDictionary(x => x.Key, x => x.Value.getId),
            setupResults.ToImmutableDictionary(x => x.Key, x => x.Value.handle),
            appliers.Build(),
            GetDefaultState,
            actorSystem);
    }
    
    protected abstract ISetupEventSourcedEventReactor<TState> Configure(
        ISetupEventSourcedEventReactor<TState> config);
    
    protected abstract TState GetDefaultState(string id);
    
    protected abstract void SetupEventAppliers(ISetupEventApplier<TState> setup);

    private class EventReactorSetup : ISetupEventSourcedEventReactor<TState>
    {
        private readonly Dictionary<Type, IEventReactorBuilder> _builders = new();

        public ISetupEventSourcedEventReactorFor<TEvent, TState> On<TEvent>(Func<TEvent, string> getId)
        {
            if (_builders.TryGetValue(typeof(TEvent), out var existingBuilder))
            {
                return (ISetupEventSourcedEventReactorFor<TEvent, TState>)existingBuilder;
            }

            var newBuilder = new EventReactorSetupFor<TEvent>(this, getId);

            _builders[typeof(TEvent)] = newBuilder;

            return newBuilder;
        }

        public IImmutableDictionary<
            Type,
            (Func<object, string> getId, Func<IReactorContext, CancellationToken, Task<IImmutableList<object>>> handle)> Build()
        {
            return _builders
                .ToImmutableDictionary(
                    x => x.Key,
                    x => (
                        x.Value.CreateGetId(),
                        x.Value.CreateReactor()));
        }

        private class EventReactorSetupFor<TEvent>(
            ISetupEventSourcedEventReactor<TState> parent,
            Func<TEvent, string> getId)
            : ISetupEventSourcedEventReactorFor<TEvent, TState>, IEventReactorBuilder
        {
            private readonly
                List<Func<EventSourcedEventReactorContext<TState>, CancellationToken, Task<IImmutableList<object>>>>
                _handlers = [];

            public ISetupEventSourcedEventReactorFor<TEvent, TState> HandleWith(
                Func<EventSourcedEventReactorContext<TState>, CancellationToken, Task<IImmutableList<object>>> handler)
            {
                _handlers.Add(handler);

                return this;
            }

            public ISetupEventSourcedEventReactorFor<TNewEvent, TState> On<TNewEvent>(
                Func<TNewEvent, string> getIdFromEvent)
            {
                return parent.On(getIdFromEvent);
            }

            public IImmutableDictionary<
                Type,
                (Func<object, string> getId, Func<IReactorContext, CancellationToken, Task<IImmutableList<object>>> handle)> Build()
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
                        var result = await handler((EventSourcedEventReactorContext<TState>)context, cancellationToken);

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

    private class EventApplierSetup : ISetupEventApplier<TState>
    {
        private readonly Dictionary<Type, Func<TState, object, TState>> _appliers = new();

        public ISetupEventApplier<TState> On<TEvent>(Func<TState, TEvent, TState> apply)
        {
            _appliers[typeof(TEvent)] = (state, evnt) => apply(state, (TEvent)evnt);

            return this;
        }

        public IImmutableDictionary<Type, Func<TState, object, TState>> Build()
        {
            return _appliers.ToImmutableDictionary();
        }
    }
}

public interface ISetupEventApplier<TState>
{
    ISetupEventApplier<TState> On<TEvent>(Func<TState, TEvent, TState> apply);
}