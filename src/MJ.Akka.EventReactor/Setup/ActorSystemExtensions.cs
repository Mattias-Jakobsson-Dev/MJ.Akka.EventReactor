using System.Collections.Immutable;
using Akka.Actor;
using MJ.Akka.EventReactor.Configuration;

namespace MJ.Akka.EventReactor.Setup;

public static class ActorSystemExtensions
{
    public static IEventReactorApplication EventReactors(
        this ActorSystem actorSystem,
        Func<IHaveConfiguration<EventReactorSystemConfig>, IHaveConfiguration<EventReactorSystemConfig>> setup)
    {
        var systemConfig = setup(EventReactorSystemSetup.From(actorSystem)).Config;

        var result = new Dictionary<string, (IConfigureEventReactor eventReactor, EventReactorConfiguration config)>();

        foreach (var reactorSetup in systemConfig.EventReactors)
        {
            result[reactorSetup.Key] = (reactorSetup.Value.eventReactor, reactorSetup.Value.setup(systemConfig));
        }
        
        return new EventReactorApplication(actorSystem, result.ToImmutableDictionary());
    }

    private record EventReactorSystemSetup(
        ActorSystem ActorSystem,
        EventReactorSystemConfig Config) : IHaveConfiguration<EventReactorSystemConfig>
    {
        public static EventReactorSystemSetup From(ActorSystem actorSystem)
        {
            return new EventReactorSystemSetup(actorSystem, EventReactorSystemConfig.Default);
        }

        public IHaveConfiguration<EventReactorSystemConfig> WithModifiedConfig(
            Func<EventReactorSystemConfig, EventReactorSystemConfig> modify)
        {
            return this with
            {
                Config = modify(Config)
            };
        }
    }

    private class EventReactorApplication(
        ActorSystem actorSystem,
        IImmutableDictionary<string, (IConfigureEventReactor eventReactor, EventReactorConfiguration config)> reactors)
        : IEventReactorApplication
    {
        public async Task<IEventReactorCoordinator> Start()
        {
            var results = new Dictionary<string, IEventReactorProxy>();

            foreach (var reactor in reactors)
            {
                var coordinator = actorSystem
                    .ActorOf(EventReactorCoordinator.Init(new SupplyReactorConfigurationInProcess(reactor.Value.config)));

                await coordinator.Ask<EventReactorCoordinator.Responses.StartResponse>(
                    new EventReactorCoordinator.Commands.Start());

                results[reactor.Key] = new ReactorProxy(coordinator);
            }

            return new Coordinator(results.ToImmutableDictionary());
        }

        private class Coordinator(IImmutableDictionary<string, IEventReactorProxy> eventReactors) 
            : IEventReactorCoordinator
        {
            public IEventReactorProxy? Get(string name)
            {
                return eventReactors.GetValueOrDefault(name);
            }
        }
        
        private class ReactorProxy(IActorRef coordinator) : IEventReactorProxy
        {
            public Task Stop()
            {
                return coordinator.Ask<EventReactorCoordinator.Responses.StopResponse>(
                    new EventReactorCoordinator.Commands.Stop());
            }

            public async Task WaitForCompletion(TimeSpan? timeout = null)
            {
                var response = await coordinator.Ask<EventReactorCoordinator.Responses.WaitForCompletionResponse>(
                    new EventReactorCoordinator.Commands.WaitForCompletion(),
                    timeout ?? Timeout.InfiniteTimeSpan);

                if (response.Error != null)
                    throw response.Error;
            }
        }
    }
}