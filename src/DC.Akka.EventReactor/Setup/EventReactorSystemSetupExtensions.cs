using System.Collections.Immutable;
using Akka.Actor;
using DC.Akka.EventReactor.Configuration;

namespace DC.Akka.EventReactor.Setup;

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
                            .Configure(EventReactorSetup.Empty)
                            .Build();

                        return new EventReactorConfiguration(
                            eventReactor,
                            instanceConfig.RestartSettings,
                            instanceConfig.CreateHandler!(eventHandlers));
                    }))
            });
    }

    private record EventReactorSetup(IImmutableDictionary<Type, Func<object, CancellationToken, Task>> Handlers)
        : ISetupEventReactor
    {
        public static EventReactorSetup Empty { get; } =
            new(ImmutableDictionary<Type, Func<object, CancellationToken, Task>>.Empty);

        public ISetupEventReactor On<TEvent>(Action<TEvent> handler)
        {
            return On<TEvent>(evnt =>
            {
                handler(evnt);

                return Task.CompletedTask;
            });
        }

        public ISetupEventReactor On<TEvent>(Func<TEvent, Task> handler)
        {
            return On<TEvent>((evnt, _) => handler(evnt));
        }

        public ISetupEventReactor On<TEvent>(Func<TEvent, CancellationToken, Task> handler)
        {
            return new EventReactorSetup(
                Handlers.SetItem(typeof(TEvent), (evnt, token) => handler((TEvent)evnt, token)));
        }

        public IImmutableDictionary<Type, Func<object, CancellationToken, Task>> Build()
        {
            return Handlers;
        }
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