using System.Collections.Immutable;

namespace MJ.Akka.EventReactor.Simple;

public abstract class SimpleEventReactor : IConfigureEventReactor
{
    public abstract string Name { get; }

    public abstract Task<IEventReactorEventSource> GetSource();

    public IReactToEvent SetupReactor()
    {
        return new ReactToEventsInProcess(Configure(new EventReactorSetup()).Build());
    }
    
    protected abstract ISetupSimpleEventReactor Configure(ISetupSimpleEventReactor config);
    
    private class EventReactorSetup : ISetupSimpleEventReactor
    {
        private readonly Dictionary<Type, IEventReactorBuilder> _builders = new();

        public ISetupSimpleEventReactorFor<TEvent> On<TEvent>()
        {
            if (_builders.TryGetValue(typeof(TEvent), out var existingBuilder))
            {
                return (ISetupSimpleEventReactorFor<TEvent>)existingBuilder;
            }

            var newBuilder = new EventReactorSetupFor<TEvent>(this);

            _builders[typeof(TEvent)] = newBuilder;

            return newBuilder;
        }

        public IImmutableDictionary<Type, Func<IReactorContext, CancellationToken, Task<IImmutableList<object>>>>
            Build()
        {
            return _builders
                .ToImmutableDictionary(
                    x => x.Key,
                    x => x.Value.CreateReactor());
        }

        private class EventReactorSetupFor<TEvent>(ISetupSimpleEventReactor parent)
            : ISetupSimpleEventReactorFor<TEvent>, IEventReactorBuilder
        {
            private readonly List<Func<EventReactorContext, CancellationToken, Task<IImmutableList<object>>>> _handlers = [];

            public ISetupSimpleEventReactorFor<TEvent> HandleWith(
                Func<EventReactorContext, CancellationToken, Task<IImmutableList<object>>> handler)
            {
                _handlers.Add(handler);

                return this;
            }

            public ISetupSimpleEventReactorFor<TNewEvent> On<TNewEvent>()
            {
                return parent.On<TNewEvent>();
            }

            public IImmutableDictionary<Type, Func<IReactorContext, CancellationToken, Task<IImmutableList<object>>>> 
                Build()
            {
                return parent.Build();
            }

            public Func<IReactorContext, CancellationToken, Task<IImmutableList<object>>> CreateReactor()
            {
                return async (context, cancellationToken) =>
                {
                    var results = ImmutableList.CreateBuilder<object>();

                    foreach (var handler in _handlers)
                    {
                        var result = await handler((EventReactorContext)context, cancellationToken);

                        results.AddRange(result);
                    }

                    return results.ToImmutable();
                };
            }
        }
        
        private interface IEventReactorBuilder
        {
            Func<IReactorContext, CancellationToken, Task<IImmutableList<object>>> CreateReactor();
        }
    }
}