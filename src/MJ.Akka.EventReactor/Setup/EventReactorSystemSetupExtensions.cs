using Akka.Actor;
using MJ.Akka.EventReactor.Configuration;

namespace MJ.Akka.EventReactor.Setup;

public static class EventReactorSystemSetupExtensions
{
    public static IHaveConfiguration<EventReactorSystemConfig> WithReactor(
        this IHaveConfiguration<EventReactorSystemConfig> source,
        IConfigureEventReactor eventReactor,
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

                        return new EventReactorConfiguration(
                            eventReactor,
                            instanceConfig.RestartSettings,
                            instanceConfig.Parallelism ?? 100,
                            instanceConfig.OutputWriters,
                            eventReactor.SetupReactor());
                    }))
            });
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