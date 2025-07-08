using System.Collections.Immutable;
using Akka.Actor;
using MJ.Akka.EventReactor.Configuration;

namespace MJ.Akka.EventReactor.Setup;

public static class EventReactorSystemSetupExtensions
{
    public static IHaveConfiguration<EventReactorSystemConfig> WithReactor(
        this IHaveConfiguration<EventReactorSystemConfig> source,
        IEventReactor eventReactor,
        Func<
            IHaveConfiguration<EventReactorInstanceConfig>,
            IHaveConfiguration<EventReactorInstanceConfig>>? setup = null)
    {
        return source
            .WithModifiedConfig(config => config with
            {
                EventReactors = config.EventReactors.SetItem(
                    eventReactor.Name,
                    (eventReactor, systemConfig =>
                    {
                        var instanceConfig = (setup ?? (s => s))(ProjectionInstanceConfigSetup.From(source.ActorSystem))
                            .Config
                            .MergeWith(systemConfig);

                        var eventHandlers = eventReactor
                            .Configure(new EventReactorSetup())
                            .Build();

                        return new EventReactorConfiguration(
                            eventReactor,
                            instanceConfig.RestartSettings,
                            instanceConfig.Parallelism ?? 100,
                            instanceConfig.OutputWriters,
                            instanceConfig.CreateHandler!(eventHandlers));
                    }))
            });
    }

    private class EventReactorSetup : ISetupEventReactor
    {
        private readonly Dictionary<Type, IEventReactorBuilder> _builders = new();
        
        public ISetupEventReactorFor<TEvent> On<TEvent>()
        {
            if (_builders.TryGetValue(typeof(TEvent), out var existingBuilder))
            {
                return (ISetupEventReactorFor<TEvent>)existingBuilder;
            }

            var newBuilder = new EventReactorSetupFor<TEvent>(this);
            
            _builders[typeof(TEvent)] = newBuilder;

            return newBuilder;
        }

        public IImmutableDictionary<Type, Func<object, CancellationToken, Task<IImmutableList<object>>>> Build()
        {
            return _builders
                .ToImmutableDictionary(
                    x => x.Key,
                    x => x.Value.CreateReactor());
        }
        
        private class EventReactorSetupFor<TEvent>(ISetupEventReactor parent) 
            : ISetupEventReactorFor<TEvent>, IEventReactorBuilder
        {
            private readonly List<Func<TEvent, CancellationToken, Task<IImmutableList<object>>>> _handlers = [];
            
            public ISetupEventReactorFor<TEvent> HandleWith(
                Func<TEvent, CancellationToken, Task<IImmutableList<object>>> handler)
            {
                _handlers.Add(handler);

                return this;
            }

            public ISetupEventReactorFor<TNewEvent> On<TNewEvent>()
            {
                return parent.On<TNewEvent>();
            }

            public IImmutableDictionary<Type, Func<object, CancellationToken, Task<IImmutableList<object>>>> Build()
            {
                return parent.Build();
            }

            public Func<object, CancellationToken, Task<IImmutableList<object>>> CreateReactor()
            {
                return async (evnt, cancellationToken) =>
                {
                    var results = ImmutableList.CreateBuilder<object>();
                    
                    foreach (var handler in _handlers)
                    {
                        var result = await handler((TEvent)evnt, cancellationToken);
                        
                        results.AddRange(result);
                    }

                    return results.ToImmutable();
                };
            }
        }
    }
    
    private interface IEventReactorBuilder
    {
        Func<object, CancellationToken, Task<IImmutableList<object>>> CreateReactor();
    }

    private record ProjectionInstanceConfigSetup(ActorSystem ActorSystem, EventReactorInstanceConfig Config)
        : IHaveConfiguration<EventReactorInstanceConfig>
    {
        public static ProjectionInstanceConfigSetup From(ActorSystem actorSystem)
        {
            return new ProjectionInstanceConfigSetup(actorSystem, EventReactorInstanceConfig.Default);
        }

        public IHaveConfiguration<EventReactorInstanceConfig> WithModifiedConfig(
            Func<EventReactorInstanceConfig, EventReactorInstanceConfig> modify)
        {
            return this with
            {
                Config = modify(Config)
            };
        }
    }
}